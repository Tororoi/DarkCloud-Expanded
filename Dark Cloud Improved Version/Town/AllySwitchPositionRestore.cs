using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Keeps the player where they were standing across an in-town ally switch.
    ///
    /// The mod implements town ally switching as a full town reload: the PNACH patches the character-menu
    /// commit at 0x1F7DB4 into <c>jal EditInit</c>, which rebuilds the town and respawns the player at the
    /// town ENTRANCE — the position reset is a side effect of that reload, not anything platform-specific
    /// (see game_data/docs/town-ally-switch-reload-re.md). This class is the researched Q2 fix:
    /// save position + facing when the allies menu opens, detect the EditInit reload, and write them back
    /// onto the freshly-loaded character while the load fade still covers the screen.
    ///
    /// Reload detection is data-only: <see cref="EditLoop.AreaFrames"/> resets to 0 when EditInit re-runs,
    /// while a REAL area change additionally sets <see cref="EditLoop.NextMapNo"/> (and changes MapNo) —
    /// so "AreaFrames reset + same map + no transition pending, shortly after the allies menu was open"
    /// can only be the ally-switch reload. Menu opened INSIDE a building is skipped entirely: the reload
    /// dumps the player outside, where interior coordinates would be nonsense.
    /// </summary>
    internal static class AllySwitchPositionRestore
    {
        internal static bool Enabled = true;

        private const string Tag = "[AllySwitchPos] ";

        /// <summary>Allies page of the pause menu (<c>Addresses.selectedMenu</c> value TownCharacter keys on).</summary>
        private const int AlliesMenu = 3;

        /// <summary>An AreaFrames reading below this counts as "the area just (re)initialized".</summary>
        private const int FreshAreaFrames = 30;

        /// <summary>How long after the allies menu closes a reload is still attributed to it. The commit
        /// runs EditInit immediately, so the reset shows up within a tick or two; 4 s is generous.</summary>
        private const double ArmSeconds = 4.0;

        /// <summary>How long to wait for the reloaded town to reach walking mode before giving up
        /// (covers a slow load or a brief arrival event).</summary>
        private const double RestoreTimeoutSeconds = 10.0;

        /// <summary>Consecutive walking-mode ticks the position is asserted on. The first write lands
        /// within ~50 ms of control returning, still inside the load fade; the extras win any race with
        /// a late entrance placement.</summary>
        private const int RestoreWrites = 3;

        /// <summary>Player-inside-a-building flag (TownCharacter's <c>buildingCheck</c> read).</summary>
        private const long InsideBuilding = 0x202A281C;

        private enum State { Idle, MenuOpen, Restoring }
        private static State _state = State.Idle;

        // Snapshot taken when the allies menu opens (the player cannot move while it is up).
        private static float _posX, _posY, _posZ, _yaw, _camAngle;
        private static bool _camValid;
        private static byte _mapNo;
        private static DateTime _armedUntil = DateTime.MinValue;

        private static DateTime _restoreDeadline;
        private static int _writesLeft;
        private static bool _prevInAllyMenu;
        private static bool _loggedTickError;

        internal static void Tick()
        {
            if (!Enabled) return;
            // Never let an exception here kill the whole town loop thread (every other town feature ticks
            // after us) — a PINE hiccup mid-reload is exactly when reads are most likely to misbehave.
            try { TickCore(); }
            catch (Exception e)
            {
                _state = State.Idle;
                if (!_loggedTickError)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message);
                    _loggedTickError = true;
                }
            }
        }

        private static void TickCore()
        {
            // Don't clear state on a non-town reading — the mode can flicker during the reload itself.
            // Stale MenuOpen/Restoring states are expired by their own deadlines on the next town tick
            // (checked BEFORE reload detection, so an old armed window can never claim a later entry).
            if (Memory.ReadByte(Addresses.mode) != 2) return;

            bool inAllyMenu = Memory.ReadByte(Addresses.selectedMenu) == AlliesMenu;
            // Snapshot ONLY on the menu-open EDGE, never while the menu sits open: after the commit the
            // menu byte keeps reading 3 for several ticks while EditInit tears the world down, and a
            // "refresh" in that window chases CharaPtr into freed memory — the snapshot becomes ~(0,0,0)
            // with a zero yaw/camera, and the restore then faithfully teleports the player to the town
            // origin with a reset camera (observed live). The edge tick is before the commit, world intact;
            // the player cannot move while the menu is open, so the edge value never goes stale.
            bool menuOpened = inAllyMenu && !_prevInAllyMenu;
            _prevInAllyMenu = inAllyMenu;

            switch (_state)
            {
                case State.Idle:
                    if (menuOpened) TrySnapshot();
                    break;

                case State.MenuOpen:
                    if (DateTime.UtcNow > _armedUntil) { _state = State.Idle; break; }
                    if (menuOpened) { TrySnapshot(); break; }   // reopen within the window → fresh snapshot
                    if (ReloadDetected())
                    {
                        _state = State.Restoring;
                        _restoreDeadline = DateTime.UtcNow.AddSeconds(RestoreTimeoutSeconds);
                        _writesLeft = RestoreWrites;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                            $"ally-switch reload detected, restoring to ({_posX:F1}, {_posY:F1}, {_posZ:F1})");
                        // Engine-side town state was rebuilt while mod caches still look valid (same
                        // addresses) — tell the per-town features that write engine state once.
                        CanalTide.OnTownReloaded();
                    }
                    break;

                case State.Restoring:
                    StepRestore();
                    break;
            }
        }

        private static void TrySnapshot()
        {
            // Inside a building the reload dumps the player OUTSIDE — interior coordinates would be
            // nonsense there, so leave those switches alone (today's behavior, no worse).
            if (Memory.ReadByte(InsideBuilding) != 0) return;
            // Never snapshot a freshly-(re)initialized area — that is the reload itself, or an entrance
            // the player only just walked in from.
            if (Memory.ReadInt(EditLoop.AreaFrames) < FreshAreaFrames) return;
            if (!EditLoop.TryReadPlayerPos(out _posX, out _posY, out _posZ)) return;
            if (!EditLoop.TryReadPlayerYaw(out _yaw)) return;
            // The world origin is the classic freed-memory reading; no real standing spot is exactly there.
            if (Math.Abs(_posX) < 0.5f && Math.Abs(_posZ) < 0.5f) return;
            if (float.IsNaN(_posX) || float.IsNaN(_posY) || float.IsNaN(_posZ) || float.IsNaN(_yaw)) return;

            long cam = FollowCamera.Base();
            _camValid = cam != 0;
            if (_camValid) _camAngle = Memory.ReadFloat(cam + FollowCamera.Angle);

            _mapNo = Memory.ReadByte(EditLoop.MapNo);
            _armedUntil = DateTime.UtcNow.AddSeconds(ArmSeconds);
            _state = State.MenuOpen;
        }

        private static bool ReloadDetected()
        {
            if (Memory.ReadInt(EditLoop.AreaFrames) >= FreshAreaFrames) return false;
            // A genuine map/area transition sets NextMapNo (255 = none pending; 1000-view is the other
            // stable reading TownCharacter accepts) — those reloads must keep their normal entrance spawn.
            byte next = Memory.ReadByte(EditLoop.NextMapNo);
            if (next != 255 && Memory.ReadUShort(EditLoop.NextMapNo) != 1000) return false;
            if (Memory.ReadByte(EditLoop.MapNo) != _mapNo) return false;
            if (Memory.ReadByte(InsideBuilding) != 0) return false;
            return true;
        }

        private static void StepRestore()
        {
            if (DateTime.UtcNow > _restoreDeadline)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "restore timed out (arrival event?), leaving entrance spawn");
                _state = State.Idle;
                return;
            }
            // The reload turned into a real transition after all → hands off.
            if (Memory.ReadByte(EditLoop.NextMapNo) != 255 && Memory.ReadUShort(EditLoop.NextMapNo) != 1000) { _state = State.Idle; return; }
            if (Memory.ReadByte(EditLoop.MapNo) != _mapNo) { _state = State.Idle; return; }

            // Wait until the town is actually walkable: the character exists and any arrival event is over.
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking) return;
            if (Memory.ReadInt(EditLoop.AreaFrames) < 2) return;

            uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) return;
            long c = Memory.ToMmu(chara);

            Memory.WriteFloat(c + EditLoop.CharaPosition, _posX);
            Memory.WriteFloat(c + EditLoop.CharaPosition + 4, _posY);
            Memory.WriteFloat(c + EditLoop.CharaPosition + 8, _posZ);
            Memory.WriteFloat(c + EditLoop.CharaRotation + 4, _yaw);

            if (_camValid)
            {
                long cam = FollowCamera.Base();
                if (cam != 0)
                {
                    // Both fields = SetAngleSoon semantics: the camera snaps behind the restored player
                    // instead of sweeping there from the entrance framing.
                    Memory.WriteFloat(cam + FollowCamera.Angle, _camAngle);
                    Memory.WriteFloat(cam + FollowCamera.AngleNow, _camAngle);
                }
            }

            if (--_writesLeft <= 0)
            {
                EditLoop.TryReadPlayerPos(out float rx, out _, out float rz);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                    $"position restored (read-back {rx:F1}, {rz:F1})");
                _state = State.Idle;
            }
        }
    }
}
