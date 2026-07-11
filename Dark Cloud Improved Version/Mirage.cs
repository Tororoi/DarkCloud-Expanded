using System;
using System.Linq;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Ungaga's "Mirage" (weapon 354): releasing a charge plants a stationary DECOY at the player's spot.
    /// While it lives (15s, refreshed by a new charge), enemies path toward the decoy instead of the
    /// player — PER ENEMY, so an enemy you HIT drops the illusion and re-targets you.
    ///
    /// Hard-won implementation (6 crashes): two independent PCSX2 limits. (1) Patching HOT, actively-
    /// executing EE code crashes the recompiler → the _GET_POSITION patch is applied ONLY at the COLD
    /// window (in-game entry, before any enemy has called it — same window the ABS patches use).
    /// (2) Executing native code from a PINE-written CAVE crashes when a fresh path is first hit → no
    /// cave stub; the redirect is a 5-word IN-PLACE rewrite (runs in _GET_POSITION's own compiled block)
    /// that reads the player pointer from a per-slot DATA table. The rewrite is UNCONDITIONAL, so a fast
    /// loop keeps the table = the live player position for un-fooled slots; fooled slots hold the decoy.
    /// </summary>
    internal static class Mirage
    {
        private const int   ChargeWindup = WeaponCollision.ActionWindup; // 0xE
        private const double DecoySeconds = 15.0;
        private const int    FastTickMs   = 25;    // table maintenance cadence while armed + in a dungeon
        private const int    IdleTickMs   = 150;

        private static bool _armed;                 // in-place patch installed this session (cold)
        private static bool _armMismatchLogged;

        private static bool     _decoyActive;
        private static DateTime _decoyDeadline;
        private static float    _dx, _dz, _dy;
        private static readonly bool[] _fooled          = new bool[MirageDecoy.MaxSlots];
        private static readonly bool[] _brokenThisDecoy = new bool[MirageDecoy.MaxSlots];
        private static int[] _prevHp;

        internal static void Start() => new Thread(Loop) { IsBackground = true }.Start();

        /// <summary>Install the in-place redirect at the COLD window (from ApplyNewChanges at in-game
        /// entry, and retried from the loop on town visits). Defers while in a dungeon (then _GET_POSITION
        /// may be hot). Fills the whole table with the current player position first so the very first
        /// enemy read after entering a dungeon is valid. Strongly location-verified before writing.</summary>
        internal static void ArmColdPatch()
        {
            if (_armed) return;
            bool ours = true;
            for (int i = 0; i < MirageDecoy.PatchNew.Length; i++)
                if ((uint)Memory.ReadInt(MirageDecoy.PatchAddr + i * 4L) != MirageDecoy.PatchNew[i]) { ours = false; break; }
            if (ours) { _armed = true; return; }

            if (Player.InDungeonFloor())
            {
                if (!_armMismatchLogged)
                    Console.WriteLine("[Mirage] in a dungeon at cold-arm time — redirect deferred to next town visit");
                _armMismatchLogged = true;
                return;
            }

            bool ok =
                (uint)Memory.ReadInt(MirageDecoy.CheckM2Addr)  == MirageDecoy.CheckM2  &&
                (uint)Memory.ReadInt(MirageDecoy.CheckBneAddr) == MirageDecoy.CheckBne &&
                (uint)Memory.ReadInt(MirageDecoy.CheckJalAddr) == MirageDecoy.CheckJal;
            for (int i = 0; i < MirageDecoy.PatchOrig.Length && ok; i++)
                if ((uint)Memory.ReadInt(MirageDecoy.PatchAddr + i * 4L) != MirageDecoy.PatchOrig[i]) ok = false;
            if (!ok)
            {
                if (!_armMismatchLogged)
                    Console.WriteLine($"[Mirage] _GET_POSITION @0x{MirageDecoy.PatchAddr:X} strong-context check FAILED — redirect disabled");
                _armMismatchLogged = true;
                return;
            }

            FillWholeTablePlayer();                                   // valid before any enemy reads it
            foreach (int i in MirageDecoy.ApplyOrder)                 // torn-safe (moot when cold, kept anyway)
                Memory.WriteUInt(MirageDecoy.PatchAddr + i * 4L, MirageDecoy.PatchNew[i]);
            _armed = true;
            Console.WriteLine($"[Mirage] redirect armed COLD (in-place @0x{MirageDecoy.PatchAddr:X})");
        }

        /// <summary>Patch the dungeon draw's chara-loop scene gate so the clone draws while enemies keep
        /// moving. At MMU 0x21DAE8A4 the gate is `beq v0,zero,skip` (word 0x10400037, skipping the 6-chara
        /// loop when iGpffff9e18==0); NOP it (0x00000000) → the loop always runs, iGpffff9e18 stays 0 so
        /// motionDrive keeps stepping enemies. Verifies the exact word before writing. The dun overlay
        /// reloads per floor, so call this at floor load (like EnemyModelInjector.Install). EXPERIMENTAL:
        /// PINE code writes can crash PCSX2 (per EnemyModelInjector) — testing whether a single beq→nop is OK.</summary>
        internal const long SceneGateAddr = 0x21DAE8A4;
        internal const uint SceneGateWord = 0x10400037;   // beq v0,zero,0x1dae984
        // The scene-gate NOP is done by the PNACH (PCSX2 applies code patches safely; PINE crashes). The
        // PNACH conditional writes 0 to 0x1DAE8A4 only while this flag == 1, so it never touches the town
        // overlay that shares the address. Mod sets it 1 while a decoy is up (in a dungeon), 0 otherwise.
        internal const long SceneGateFlag = 0x21F10038;   // guest 0x01F10038 (mod PNACH mailbox; free slot)
        internal static void ApplySceneDrawPatch()
        {
            uint cur = (uint)Memory.ReadInt(SceneGateAddr);
            if (cur == 0x00000000) { Console.WriteLine("[Mirage] scene-draw gate already NOP'd"); return; }
            if (cur != SceneGateWord) { Console.WriteLine($"[Mirage] scene-draw gate word 0x{cur:X8} != 0x{SceneGateWord:X8} — NOT patching"); return; }
            Memory.WriteInt(SceneGateAddr, 0x00000000);
            uint back = (uint)Memory.ReadInt(SceneGateAddr);
            Console.WriteLine($"[Mirage] scene-draw gate patched 0x{SceneGateWord:X8}→0x{back:X8} (chara loop always runs)");
        }

        private static void Loop()
        {
            bool prevGuard = false;
            while (true)
            {
                int sleep = IdleTickMs;
                try
                {
                    bool inDun = Player.InDungeonFloor();
                    if (!_armed && !inDun) ArmColdPatch();

                    // One-shot residency check OUTSIDE a dungeon (town/menu) — is the dun overlay resident in
                    // the cold window? word 0x10400037 present at 0x21DAE8A4 → yes, a main-menu cold patch works.
                    if (_armed && !inDun && !_sceneVerified)
                    {
                        _sceneVerified = true;
                        Console.WriteLine("[Mirage/patch] residency check in TOWN/MENU (not dungeon):");
                        VerifySceneGate();
                    }

                    if (_armed && inDun)
                    {
                        if (!_sceneVerifiedDun) { _sceneVerifiedDun = true; Console.WriteLine("[Mirage/patch] IN-DUNGEON probe:"); VerifySceneGate(); }

                        // Keep un-fooled slots on the live player (the patch reads the table for EVERY
                        // enemy, always) and fooled slots on the decoy — one batched write per fast tick.
                        bool ungagaMirage = Player.Weapon.GetCurrentWeaponId() == Items.mirage &&
                                            Player.CurrentCharacterNum() == Player.UngagaId;

                        if (ungagaMirage && !Player.CheckDunIsPaused())
                        {
                            // Plant the decoy when the player raises GUARD (R1 press = rising edge), refreshed
                            // by re-guarding — replaces the old charge-release trigger.
                            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
                            if (guarding && !prevGuard) PlaceDecoy();
                            prevGuard = guarding;
                            if (_decoyActive) UpdateDecoyState();
                        }
                        else if (_decoyActive && Player.CheckDunIsPaused())
                        {
                            _decoyDeadline = _decoyDeadline.AddMilliseconds(FastTickMs);
                        }
                        else if (!ungagaMirage && _decoyActive)
                        {
                            _decoyActive = false; Array.Clear(_fooled, 0, _fooled.Length);
                            DespawnClone();
                        }

                        WriteTable();
                        sleep = FastTickMs;
                    }
                    else
                    {
                        prevGuard = false;
                        if (_decoyActive || _cloneSlot >= 0) { _decoyActive = false; DespawnClone(); }
                    }
                }
                catch (Exception e) { Console.WriteLine("[Mirage] tick failed: " + e.Message); }
                Thread.Sleep(sleep);
            }
        }

        // ── decoy state (all DATA) ───────────────────────────────────────────────────────────
        private static void PlaceDecoy()
        {
            _dx = Memory.ReadFloat(Addresses.dunPositionX);
            _dz = Memory.ReadFloat(Addresses.dunPositionZ);
            _dy = Memory.ReadFloat(Addresses.dunPositionY);
            Array.Clear(_fooled, 0, _fooled.Length);
            Array.Clear(_brokenThisDecoy, 0, _brokenThisDecoy.Length);
            _prevHp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MirageDecoy.MaxSlots; s++)
                if (IsLiveEnemy(s)) _fooled[s] = true;
            _decoyActive = true;
            _decoyDeadline = DateTime.UtcNow.AddSeconds(DecoySeconds);
            if (!_charaDiagDone) { _charaDiagDone = true; DumpCharaSystem(); }   // is the individual-NPC (chara) system live in combat?
            DespawnClone();   // clear any stale slot from a previous decoy
            SpawnClone();
            if (_cloneSlot >= 0) Memory.WriteInt(SceneGateFlag, 1);   // arm the PNACH scene-gate NOP (clone draws)
            Console.WriteLine($"[Mirage] decoy planted at ({_dx:0.#},{_dy:0.#}); enemies redirected");
        }

        private static void UpdateDecoyState()
        {
            if (DateTime.UtcNow > _decoyDeadline)
            {
                _decoyActive = false; Array.Clear(_fooled, 0, _fooled.Length);
                DespawnClone();
                Console.WriteLine("[Mirage] decoy faded; enemies re-target the player");
                return;
            }
            MaintainClone();
            int[] hp = ReusableFunctions.GetEnemiesHp();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count && s < MirageDecoy.MaxSlots; s++)
            {
                if (!IsLiveEnemy(s)) { _fooled[s] = false; continue; }
                if (_prevHp != null && s < _prevHp.Length && hp[s] < _prevHp[s])
                { _fooled[s] = false; _brokenThisDecoy[s] = true; continue; }
                if (!_fooled[s] && !_brokenThisDecoy[s]) _fooled[s] = true;
            }
            _prevHp = hp;
        }

        /// <summary>One batched write of the managed slots: fooled → decoy, otherwise → live player.</summary>
        private static void WriteTable()
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX);
            float pz = Memory.ReadFloat(Addresses.dunPositionZ);
            float py = Memory.ReadFloat(Addresses.dunPositionY);
            var buf = new byte[MirageDecoy.MaxSlots * MirageDecoy.SlotStride];
            for (int s = 0; s < MirageDecoy.MaxSlots; s++)
            {
                int b = s * MirageDecoy.SlotStride;
                float x = _fooled[s] ? _dx : px, z = _fooled[s] ? _dz : pz, y = _fooled[s] ? _dy : py;
                BitConverter.GetBytes(x).CopyTo(buf, b);
                BitConverter.GetBytes(z).CopyTo(buf, b + 4);
                BitConverter.GetBytes(y).CopyTo(buf, b + 8);
                BitConverter.GetBytes(1.0f).CopyTo(buf, b + 12);
            }
            Memory.WriteBytesBatch(MirageDecoy.TableAddr, buf);
        }

        private static void FillWholeTablePlayer()
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX);
            float pz = Memory.ReadFloat(Addresses.dunPositionZ);
            float py = Memory.ReadFloat(Addresses.dunPositionY);
            var buf = new byte[MirageDecoy.TableSlots * MirageDecoy.SlotStride];
            for (int s = 0; s < MirageDecoy.TableSlots; s++)
            {
                int b = s * MirageDecoy.SlotStride;
                BitConverter.GetBytes(px).CopyTo(buf, b);
                BitConverter.GetBytes(pz).CopyTo(buf, b + 4);
                BitConverter.GetBytes(py).CopyTo(buf, b + 8);
                BitConverter.GetBytes(1.0f).CopyTo(buf, b + 12);
            }
            Memory.WriteBytesBatch(MirageDecoy.TableAddr, buf);
        }

        private static bool IsLiveEnemy(int s)
        {
            int id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.EnemySpeciesId));
            if (id == 0 || id == 0xFFFF) return false;
            return Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp)) > 0;
        }

        // ── clone visual: a copy of the player CCharacter placed in a free NPC draw slot (the game draws
        //    it for us; see MirageClone). No injected code — pure data. ──────────────────────────────────
        private const bool  CloneEnabled = true;         // MotionParts host: single character-draw slot with its own texture group
        private const bool  GhostSilhouette = true;      // translucent character clone
        private const bool  DuplicateTextures = false;   // not needed: PNACH redirects chara[0]'s reload to the player's own group 0x1d
        private const bool  RetagTextures     = false;   // not needed: textures shared by GROUP (clone reloads 0x1d, same as the weapon)
        private const float GhostOpacity = 90f;          // of 128; lower = more see-through
        private const float GhostDim     = 1.0f;         // full lighting so any resident character texture shows
        private const bool  CloneUseEnemyModel = false;
        private const bool  MonsterHijackTest = false;   // retired: live-enemy hijack (texture + stability walls)
        private const bool  HijackActualRoot = false;
        private static int  _hijackSlot = -1;
        private static uint _hijackOrigRoot;
        private const float DimStart = 1.0f;   // TEST: full-bright, no fade — isolate positioning from the dim
        private const float DimEnd   = 1.0f;
        private const float CloneOpacity = 96f;   // slightly translucent (of the 128 opaque default); fades → 0 over the decoy life
        private static int _cloneSlot = -1;   // NPC type slot we borrowed, or -1
        private static int _cloneLogTick;
        private static float _pfX, _pfH, _pfY; // PosField we last wrote (for the reserved-base correction)
        private static uint _srcModelRoot;    // the model root the clone copies/renders (player or test enemy)
        private static uint _cloneRootGuest;  // guest addr of the deep-copied clone root (node pool slot 0)
        private static int  _cloneNodeCount;  // nodes in the last deep copy
        private static byte[] _savedLabel100; // host enemy's original label-100 AI bytes (restored on despawn)
        private static long   _savedLabel100Addr;
        private static byte[] _cloneBuf;      // cached CCharacter bytes for the NPC clone (re-asserted each tick)

        /// <summary>The source model root for the clone: the player's, or (TEST) a live enemy's — to check
        /// whether the NPC slot can render ANY geometry at the decoy.</summary>
        private static uint CloneSourceModelRoot()
        {
            if (CloneUseEnemyModel)
                for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
                {
                    if (!IsLiveEnemy(s)) continue;
                    uint r = (uint)Memory.ReadInt(EnemyAddresses.CharObjects.CharAddr(s) + MirageClone.CharModel);
                    if (r != 0 && (r & 0x1FFFFFFF) < 0x02000000)
                    { Console.WriteLine($"[Mirage] TEST: cloning enemy slot {s} model @0x{r:X}"); return r; }
                }
            return (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.CharModel);
        }

        private static void LogClone(string tag)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX);
            float ph = Memory.ReadFloat(Addresses.dunPositionZ);
            float py = Memory.ReadFloat(Addresses.dunPositionY);
            Console.WriteLine($"[Mirage/{tag}] player=({px:0.#},{ph:0.#},{py:0.#}) decoy=({_dx:0.#},{_dz:0.#},{_dy:0.#}) npcType={_cloneSlot} root=0x{_cloneRootGuest:X} nodes={_cloneNodeCount}");
        }

        private static long DngMapInstance()
        {
            long native = (uint)Memory.ReadInt(MirageClone.NowDngMapPtr) & 0x1FFFFFFF;
            return (native == 0 || native >= 0x02000000) ? 0 : Memory.ToMmu(native);
        }

        private static void SpawnClone()
        {
            if (!CloneEnabled) return;

            // Independent deep copy of the player's whole frame tree.
            if (!BuildCloneTree()) { _cloneSlot = -1; return; }

            // Host = chara slot 0, drawn by the DUNGEON scene draw (Draw__11CSeireiKing) as a single
            // Draw__12CNPCharacter with its own texture group (0x20). Fill the player's draw fields, aim the
            // model at the clone tree, freeze, set position/opacity, mark active, and register it for draw.
            _cloneSlot = 0;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;

            byte[] buf = Memory.ReadBytesBatch(MirageClone.PlayerChar, MirageClone.CharCopySize);
            if (buf == null) { _cloneSlot = -1; return; }
            BitConverter.GetBytes((uint)MirageClone.ClothStubGuest).CopyTo(buf, MirageClone.ClothList);
            BitConverter.GetBytes(GhostSilhouette ? GhostDim : 1.0f).CopyTo(buf, MirageClone.DimFactor);
            BitConverter.GetBytes(-1).CopyTo(buf, MirageClone.MotionId);   // freeze (body mirrors via shared bones)
            BitConverter.GetBytes(GhostSilhouette ? GhostOpacity : 128f).CopyTo(buf, MirageClone.NpcOpacity);  // +0xcec (Draw needs >0)
            for (int o = MirageClone.LightFrom; o < MirageClone.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            // Keep the copied tex-anime object (+0xdc) intact — it selects the face/skin textures; zeroing it
            // left the face dark and arms mis-textured. (Was zeroed for the old shared-texture NPC host.)
            BitConverter.GetBytes(_dx).CopyTo(buf, MirageClone.CharPos);       // +0x10 X
            BitConverter.GetBytes(_dz).CopyTo(buf, MirageClone.CharPos + 4);   // +0x14 Z/height
            BitConverter.GetBytes(_dy).CopyTo(buf, MirageClone.CharPos + 8);   // +0x18 Y
            BitConverter.GetBytes(_cloneRootGuest).CopyTo(buf, MirageClone.CharModel);

            Memory.WriteBytesBatch(MirageClone.ClothStub, new byte[16]);
            Memory.WriteBytesBatch(slot, buf);   // fill the CCharacter fields (0..0xD60)
            Memory.WriteInt(slot + MirageClone.CharaActive, 1);    // active (Draw__12CNPCharacter gate; beyond 0xD60)
            Memory.WriteInt(slot + MirageClone.CharaMotionA, 0);   // no motion step (Step__12CNPCharacter skips)
            Memory.WriteInt(slot + MirageClone.CharaMotionB, 0);
            Memory.WriteInt(MirageClone.CharaRegistry + (long)_cloneSlot * 4, 1);   // register → dungeon draw draws it
            if (DuplicateTextures) DuplicateCharaTextures(MirageClone.PlayerTexGroup, MirageClone.CharaTexBase + _cloneSlot);   // copy char textures → clone's group
            else if (RetagTextures) RetagGroupToClone(MirageClone.PlayerTexGroup, MirageClone.CharaTexBase + _cloneSlot);       // retag char textures → clone's group (array full)
            Console.WriteLine($"[Mirage] clone in chara slot {_cloneSlot} (texgroup 0x{MirageClone.CharaTexBase + _cloneSlot:X}) @({_dx:0.#},{_dz:0.#},{_dy:0.#}) root=0x{_cloneRootGuest:X}");
        }

        private const long TexMgr = 0x21C75870;
        private static readonly System.Collections.Generic.List<(long addr, int orig)> _retagged =
            new System.Collections.Generic.List<(long, int)>();

        /// <summary>Re-tag the ACTIVE character's texture entries (names cNN.. — body cNNa*, weapon cNNw*)
        /// into the clone's NPC group, so DrawNPCDraw's ReloadTexture(group) actually reloads the character
        /// textures to their own VRAM slots — texturing the clone AND restoring the character VRAM the
        /// enemies overwrite. Reversible (originals restored on despawn); safe for the player because its
        /// textures are resident (its own Draw never reloads by group). Also force loaded=0 so the reload
        /// uploads the full set.</summary>
        private static void RetagCharacterTextures(int group)
        {
            _retagged.Clear();
            int count = Memory.ReadInt(TexMgr);
            if (count <= 0 || count > 4096) return;
            int done = 0;
            for (int i = 1; i <= count && done < 32; i++)
            {
                long e = TexMgr + 0x1148 + (long)(i - 1) * 0x50;
                int w0 = Memory.ReadInt(e);
                byte[] nb = Memory.ReadBytesBatch(e + 8, 4);
                if (nb == null) continue;
                // character texture = 'c' + digit + digit (cNN…); enemies are 'e', UI/dungeon differ
                if ((nb[0] == 'c' || nb[0] == 'C') && nb[1] >= '0' && nb[1] <= '9' && nb[2] >= '0' && nb[2] <= '9')
                {
                    _retagged.Add((e, w0));
                    Memory.WriteInt(e, (w0 & unchecked((int)0xFFFF0000)) | (group & 0xFFFF));
                    done++;
                }
            }
            Memory.WriteInt(TexMgr + (long)group * 0xF * 4 + 0x10 * 4, 0);   // loaded=0 → full reload
            Console.WriteLine($"[Mirage] re-tagged {done} character textures → group {group}");
        }

        private static void RestoreTaggedTextures()
        {
            foreach (var (addr, orig) in _retagged) Memory.WriteInt(addr, orig);
            _retagged.Clear();
        }

        /// <summary>DIAGNOSTIC: dump the CTextureManager texture list — a per-group histogram plus the
        /// entries tagged with the clone's NPC group (the stale textures ReloadTexture uploads). Entries
        /// start at texMgr+0x1148, stride 0x50; group = short@+0, TEX0 reg = u64@+0x28 (encodes the GS
        /// buffer/CLUT base). Count at texMgr+0.</summary>
        private static void DumpTextureList(int cloneGroup)
        {
            const long tm = 0x21C75870;
            int count = Memory.ReadInt(tm);
            if (count <= 0 || count > 4096) { Console.WriteLine($"[Mirage/tex] bad count {count}"); return; }
            Console.WriteLine($"[Mirage/tex] count={count} cloneGroup={cloneGroup} — name(group) per entry:");
            // entry i (1..count) at tm + 0x1148 + (i-1)*0x50; group short @+0, name @+8, TEX0 @+0x28
            var sb = new System.Text.StringBuilder("[Mirage/tex] ");
            int lineN = 0;
            for (int i = 1; i <= count; i++)
            {
                long e = tm + 0x1148 + (long)(i - 1) * 0x50;
                int g = (short)(Memory.ReadInt(e) & 0xFFFF);
                byte[] nb = Memory.ReadBytesBatch(e + 8, 16);
                string name = "";
                if (nb != null) { int n = 0; while (n < 16 && nb[n] >= 0x20 && nb[n] < 0x7F) n++; name = System.Text.Encoding.ASCII.GetString(nb, 0, n); }
                sb.Append($"{name}(g{g}) ");
                if (++lineN >= 8) { Console.WriteLine(sb.ToString()); sb.Clear(); sb.Append("[Mirage/tex] "); lineN = 0; }
            }
            if (lineN > 0) Console.WriteLine(sb.ToString());
        }

        /// <summary>Copy the player's root CFrame node into the cave as the clone's own root. Its child
        /// pointer already targets the shared bone subtree (from the copy); zero its sibling so the clone
        /// root draws just that subtree. Returns false if the player root isn't resolvable.</summary>
        private static bool BuildCloneRoot()
        {
            _srcModelRoot = CloneSourceModelRoot();
            if (_srcModelRoot == 0 || (_srcModelRoot & 0x1FFFFFFF) >= 0x02000000) return false;
            byte[] node = Memory.ReadBytesBatch(Memory.ToMmu(_srcModelRoot & 0x1FFFFFFF), MirageClone.RootCopySize);
            if (node == null) return false;
            BitConverter.GetBytes(0).CopyTo(node, MirageClone.RootSibling);   // clone root has no sibling
            Memory.WriteBytesBatch(MirageClone.RootBuf, node);
            return true;
        }

        /// <summary>Deep-copy the source model's ENTIRE CFrame tree into the cave node pool, replicating the
        /// game's own CopyFrameVu1 (0x127610): 0x270 bytes/node, links (parent/child/sibling) fixed up to
        /// the copied nodes via an old→new map, geometry (+0x260) SHARED. The result is an INDEPENDENT posed
        /// model whose world matrices resolve up its OWN parent chain (GetLWMatrix walks +0x110), so it
        /// renders wherever its host CCharacter places it — without dragging or corrupting the player.
        /// Returns false if the tree isn't resolvable or overflows the pool.</summary>
        private static bool BuildCloneTree()
        {
            _srcModelRoot = CloneSourceModelRoot();
            uint rootPhys = _srcModelRoot & 0x1FFFFFFF;
            if (_srcModelRoot == 0 || rootPhys == 0 || rootPhys >= 0x02000000) return false;

            // DFS collect nodes exactly as CopyFrameVu1 recurses: a node, then every node in its child-list
            // (child, child.sibling, …). The ROOT's own sibling is never followed (it's a different model).
            var order = new System.Collections.Generic.List<uint>();
            var map   = new System.Collections.Generic.Dictionary<uint, int>();
            var work  = new System.Collections.Generic.Stack<uint>();
            work.Push(rootPhys);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (n == 0 || n >= 0x02000000 || map.ContainsKey(n)) continue;
                if (order.Count >= MirageClone.MaxNodes)
                { Console.WriteLine($"[Mirage] clone tree > {MirageClone.MaxNodes} nodes — aborting"); return false; }
                map[n] = order.Count; order.Add(n);
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }

            // Copy + fix up each node into the pool.
            for (int i = 0; i < order.Count; i++)
            {
                byte[] nb = Memory.ReadBytesBatch(Memory.ToMmu(order[i]), MirageClone.NodeSize);
                if (nb == null) return false;
                FixLink(nb, MirageClone.Parent,      map, i == 0);   // root's parent → 0 (top-level frame)
                FixLink(nb, MirageClone.RootChild,   map, false);
                FixLink(nb, MirageClone.RootSibling, map, i == 0);   // root's sibling → 0
                BitConverter.GetBytes(0).CopyTo(nb, MirageClone.WorldCacheA);   // force world recompute
                BitConverter.GetBytes(0).CopyTo(nb, MirageClone.WorldCacheB);
                // geometry @+0x260 left as-is (shared mesh)
                Memory.WriteBytesBatch(MirageClone.NodePool + (long)i * MirageClone.NodeStride, nb);
            }

            _cloneNodeCount = order.Count;
            _cloneRootGuest = (uint)MirageClone.NodePoolGuest;   // slot 0 = root
            Console.WriteLine($"[Mirage] deep-copied {order.Count}-node clone tree → root 0x{_cloneRootGuest:X}");
            return true;
        }

        /// <summary>Rewrite a node pointer field to the copied node (via the old→new map), or 0 when the
        /// target is outside the tree (external ref) or forceZero (the root's parent/sibling).</summary>
        private static void FixLink(byte[] nb, int off, System.Collections.Generic.Dictionary<uint, int> map, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(nb, off) & 0x1FFFFFFF;
            uint neu = 0;
            if (!forceZero && old != 0 && map.TryGetValue(old, out int idx))
                neu = (uint)(MirageClone.NodePoolGuest + (long)idx * MirageClone.NodeStride);
            BitConverter.GetBytes(neu).CopyTo(nb, off);
        }

        private static void MaintainClone()
        {
            if (_cloneSlot < 0) return;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;
            // Hold at the decoy, keep the model + active/opacity set, and keep it registered for draw
            // (the game may reset the registry / opacity-ramp between our ticks).
            Memory.WriteFloat(slot + MirageClone.CharPos,     _dx);
            Memory.WriteFloat(slot + MirageClone.CharPos + 4, _dz);
            Memory.WriteFloat(slot + MirageClone.CharPos + 8, _dy);
            Memory.WriteUInt (slot + MirageClone.CharModel,   _cloneRootGuest);

            Memory.WriteInt  (slot + MirageClone.CharaActive, 1);
            Memory.WriteFloat(slot + MirageClone.NpcOpacity,  GhostSilhouette ? GhostOpacity : 128f);
            Memory.WriteInt  (MirageClone.CharaRegistry + (long)_cloneSlot * 4, 1);

            // NOTE: iGpffff9e18 (0x202A3608) is the "scene active" flag — setting it draws the chara loop BUT
            // freezes enemies (motionDrive steps enemies only when it's 0). So we DON'T set it here; the clone
            // is drawn instead via an in-place patch of Draw__11CSeireiKing's chara-loop gate (SceneDrawPatch),
            // which lets the loop run while the flag stays 0 so enemies keep moving.
            _cloneLogTick++;
        }

        private static void DespawnClone()
        {
            Memory.WriteInt(SceneGateFlag, 0);   // disarm the PNACH scene-gate NOP (always — keeps town safe)
            if (_cloneSlot < 0) return;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;
            Memory.WriteInt (MirageClone.CharaRegistry + (long)_cloneSlot * 4, 0);   // unregister
            Memory.WriteInt (slot + MirageClone.CharaActive, 0);                     // inactive
            Memory.WriteUInt(slot + MirageClone.CharModel, 0);                       // clear model
            RemoveDuplicateTextures();                                               // drop the appended texture dups
            RestoreRetag();                                                          // restore any retagged group tags
            _cloneSlot = -1;
        }

        private static void WriteCloneVec(long addr, float x, float z, float y)
        {
            Memory.WriteFloat(addr, x); Memory.WriteFloat(addr + 4, z); Memory.WriteFloat(addr + 8, y);
        }

        /// <summary>Set an enemy slot's render opacity (FloorSlot+0x120; 128 = opaque). We pause the engine's
        /// own fade (OpacityFadeGate=1, OpacityFadeStep=0) so PalletStep doesn't fight our value, then write
        /// the level directly — the mod owns the clone's translucency and its fade-to-0 ramp.</summary>
        private static bool _texDiagDone;
        private static bool _charaDiagDone;
        private static bool _gateScanned;
        private static bool _sceneVerified;
        private static bool _sceneVerifiedDun;

        /// <summary>READ-ONLY: verify the dungeon overlay's chara-loop gate instruction is where we think, at
        /// guest 0x1dae8a4 (MMU 0x21DAE8A4) and at +0x80 (the known dun-overlay header-shift ambiguity).
        /// Expected around the gate: lw v0,-0x61e8(gp)=189e828f, beq v0,zero=37004010, nop, clr s0=28860070.
        /// Whichever address shows that pattern is the real NOP target (beq→00000000).</summary>
        private static void VerifySceneGate()
        {
            void Dump(long a)
            {
                byte[] b = Memory.ReadBytesBatch(a, 16);
                if (b == null) { Console.WriteLine($"[Mirage/patch] {a:X}: (unreadable)"); return; }
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 16; i += 4) sb.Append(BitConverter.ToUInt32(b, i).ToString("X8")).Append(' ');
                Console.WriteLine($"[Mirage/patch] 0x{a:X}: {sb}");
            }
            Console.WriteLine("[Mirage/patch] verifying chara-loop gate (want: 189E828F 37004010 00000000 28860070)");
            Dump(0x21DAE8A0);          // no shift
            Dump(0x21DAE8A0 + 0x80);   // +0x80 header shift

            // ALT HOST probe: Draw__11CSeireiKing also draws a fixed CCharacter 0x1EA2ED0 in combat, gated only
            // by DATA (*(iGpffff9c78+4)==1; iGpffff9c78 = gp-0x6388 = 0x202A3468) — NOT the scene flag. If it's
            // an empty slot here, it's a data-only combat host (no code patch, no enemy freeze).
            uint altRoot = (uint)Memory.ReadInt(0x21EA2ED0 + 0xBC);   // model root of 0x1ea2ed0
            uint altVt   = (uint)Memory.ReadInt(0x21EA2ED0 + 0xA0);
            uint c78     = (uint)Memory.ReadInt(0x202A3468);          // iGpffff9c78 ptr
            int  flag    = (c78 != 0 && (c78 & 0x1FFFFFFF) < 0x02000000) ? Memory.ReadByte(Memory.ToMmu(c78 & 0x1FFFFFFF) + 4) : -1;
            Console.WriteLine($"[Mirage/alt] 0x1EA2ED0 root=0x{altRoot:X} vtable=0x{altVt:X}; iGpffff9c78=0x{c78:X} +4flag={flag}");
        }

        /// <summary>One-shot probe of the individual-NPC (chara) system to see if it's usable during combat:
        /// chara array base DAT_01d3d228 (stride 0x14a0, count DAT_01d3d3c8), the GetChara(-1) default
        /// DAT_01d3d21c, and the per-frame draw-registry count DAT_01d3d230. If the array is allocated and
        /// the default chara has a valid vtable/model root, we can host a single clone here (no crowd).</summary>
        private static void DumpCharaSystem()
        {
            long arr    = (uint)Memory.ReadInt(0x21D3D228);   // chara array base (guest ptr)
            int  cnt    = Memory.ReadInt(0x21D3D3C8);          // chara count
            long def    = (uint)Memory.ReadInt(0x21D3D21C);    // GetChara(-1) default chara
            int  regCnt = Memory.ReadInt(0x21D3D230);          // per-frame NPC draw-registry count
            Console.WriteLine($"[Mirage/chara] arr=0x{arr:X} count={cnt} regCount={regCnt} default=0x{def:X} stride=0x14A0");
            void Probe(string tag, long guest)
            {
                uint g = (uint)guest & 0x1FFFFFFF;
                if (g == 0 || g >= 0x02000000) { Console.WriteLine($"[Mirage/chara]   {tag}: (null/invalid 0x{guest:X})"); return; }
                long m = Memory.ToMmu(g);
                uint vt = (uint)Memory.ReadInt(m + 0xA0);
                uint root = (uint)Memory.ReadInt(m + 0xBC);
                Console.WriteLine($"[Mirage/chara]   {tag} @0x{guest:X}: vtable=0x{vt:X} modelRoot=0x{root:X}");
            }
            Probe("default(player)", def);
            if (arr != 0 && (arr & 0x1FFFFFFF) < 0x02000000)
                for (int i = 0; i < cnt && i < 4; i++) Probe($"chara[{i}]", arr + (long)i * 0x14A0);

            // MotionParts: MainDraw's 4-slot CHARACTER draw loop — each slot drawn via vtable+0xac with its
            // OWN texture group (0x1b+i) reloaded first. If the player is a slot and others are empty, an
            // empty slot is the host we want (correct per-character textures, single instance).
            uint mpPtr = (uint)Memory.ReadInt(0x212A28EC);   // pointer form of MotionParts
            Console.WriteLine($"[Mirage/mp] arrayA=0x01D4F030 ptrB=0x{mpPtr:X} (array=guest+i*0x11B0, texgroup=0x1b+i)");
            for (int i = 0; i < 4; i++) Probe($"MP.A[{i}] tex0x{0x1b + i:X}", 0x01D4F030 + (long)i * 0x11B0);
            if (mpPtr != 0 && (mpPtr & 0x1FFFFFFF) < 0x02000000)
                for (int i = 0; i < 4; i++) Probe($"MP.B[{i}]", (long)mpPtr + (long)i * 0x11B0);
        }

        /// <summary>One-shot diagnostic: per-group texture summary — count, first-name initial (c=character,
        /// e=enemy), VRAM base (TEX0 &amp; 0x3FFF at entry+0x28), and the group's size/loaded/priority
        /// metadata (CTextureManager[g*0xF + {0xF,0x10,0x12}]). Tells us whether the player 'c' group and the
        /// enemy 'e'/current group share VRAM (→ physical conflict) or not (→ co-residence is cheap).</summary>
        private static void DumpTextureDiag()
        {
            const long tm = 0x21C75870;
            int count = Memory.ReadInt(tm);
            int curGroup = Memory.ReadInt(tm + 0xC);
            if (count <= 0 || count > 8192) { Console.WriteLine($"[Mirage/tex] bad count {count}"); return; }
            var groups = new System.Collections.Generic.Dictionary<int, (int n, char kind, int vram)>();
            for (int i = 1; i <= count; i++)
            {
                long e = tm + 0x1148 + (long)(i - 1) * 0x50;
                int g = (short)(Memory.ReadInt(e) & 0xFFFF);
                byte[] nb = Memory.ReadBytesBatch(e + 8, 8);
                char k = (nb != null && nb[0] >= 0x20 && nb[0] < 0x7F) ? (char)nb[0] : '?';
                int vram = Memory.ReadInt(e + 0x28) & 0x3FFF;   // GS VRAM buffer base (word address)
                if (!groups.TryGetValue(g, out var v)) v = (0, k, vram);
                groups[g] = (v.n + 1, v.kind, v.vram);
            }
            Console.WriteLine($"[Mirage/tex] count={count} currentGroup={curGroup}  (g=group, first-name-initial, vram=TEX0 base word)");
            foreach (var kv in groups.OrderBy(k => k.Key))
            {
                int g = kv.Key;
                int size   = Memory.ReadInt(tm + ((long)g * 0xF + 0xF)  * 4);
                int loaded = Memory.ReadInt(tm + ((long)g * 0xF + 0x10) * 4);
                int prio   = Memory.ReadInt(tm + ((long)g * 0xF + 0x12) * 4);
                Console.WriteLine($"[Mirage/tex] g{g,-3} n={kv.Value.n,-3} first='{kv.Value.kind}' vram=0x{kv.Value.vram:X4} size={size} loaded={loaded} prio={prio}");
            }
        }

        private static int _dupCount;   // # of duplicated texture entries currently appended (for cleanup)

        /// <summary>Duplicate the active character's texture entries (group <paramref name="fromGroup"/> =
        /// the dungeon player group 0x1d) into the clone's chara group <paramref name="toGroup"/> = 0x20, so
        /// the dungeon's ReloadTexture(0x20) uploads the character's textures for the clone. Appends NEW
        /// texmgr entries (same image source / TEX0, group changed); the originals stay in 0x1d so the real
        /// player is unaffected. Entries: texMgr+0x1148 stride 0x50 (group short @+0). Removed on despawn.</summary>
        private const int TexMgrMax = 0xC4;   // 196 CTexture slots (Initialize__15CTextureManager inits 0xC4) — HARD cap
        private static void DuplicateCharaTextures(int fromGroup, int toGroup)
        {
            const long tm = 0x21C75870;
            RemoveDuplicateTextures();
            int count = Memory.ReadInt(tm);
            if (count <= 0 || count >= TexMgrMax) { Console.WriteLine($"[Mirage] texmgr {count}/{TexMgrMax} — no room to duplicate"); return; }
            int free = TexMgrMax - 1 - count;   // 1-slot margin; NEVER exceed the array (overflow crashed before)
            if (free <= 0) { Console.WriteLine($"[Mirage] texmgr {count}/{TexMgrMax} — full"); return; }
            var dups = new System.Collections.Generic.List<byte[]>();
            for (int i = 1; i <= count && dups.Count < free; i++)
            {
                long e = tm + 0x1148 + (long)(i - 1) * 0x50;
                if ((short)(Memory.ReadInt(e) & 0xFFFF) != fromGroup) continue;
                byte[] entry = Memory.ReadBytesBatch(e, 0x50);
                if (entry == null) continue;
                entry[0] = (byte)(toGroup & 0xFF); entry[1] = (byte)((toGroup >> 8) & 0xFF);   // group short @+0
                dups.Add(entry);
            }
            if (dups.Count == 0) { Console.WriteLine($"[Mirage] no group 0x{fromGroup:X} textures to duplicate"); return; }
            for (int k = 0; k < dups.Count; k++)   // write entries FIRST, verify each, bump count LAST
            {
                long e = tm + 0x1148 + (long)(count + k) * 0x50;
                Memory.WriteBytesBatch(e, dups[k]);
                byte[] back = Memory.ReadBytesBatch(e, 2);
                if (back == null || back[0] != dups[k][0] || back[1] != dups[k][1]) { Console.WriteLine("[Mirage] dup write unverified — aborting"); return; }
            }
            Memory.WriteInt(tm, count + dups.Count);                       // bump count LAST (only complete entries visible)
            Memory.WriteInt(tm + ((long)toGroup * 0xF + 0x10) * 4, 0);     // group loaded=0 → full reload
            _dupCount = dups.Count;
            Console.WriteLine($"[Mirage] duplicated {_dupCount}/{free} free textures 0x{fromGroup:X}→0x{toGroup:X} (count {count}→{count + _dupCount}/{TexMgrMax})");
        }

        private static void RemoveDuplicateTextures()
        {
            if (_dupCount <= 0) return;
            const long tm = 0x21C75870;
            int count = Memory.ReadInt(tm);
            if (count >= _dupCount) Memory.WriteInt(tm, count - _dupCount);   // drop the appended dups (they're last)
            _dupCount = 0;
        }

        private static readonly System.Collections.Generic.List<long> _retagEntries = new System.Collections.Generic.List<long>();
        private static readonly System.Collections.Generic.List<int>  _retagOrig   = new System.Collections.Generic.List<int>();

        /// <summary>Retag the active character's BODY textures (name 'c10a…', in groups 0x10/0x11) to the
        /// clone's chara group <paramref name="toGroup"/> (0x20) so ReloadTexture(0x20) loads them for the
        /// clone. Skips the WEAPON textures ('c10w…', group 0x1d) so the real weapon stays intact. No new
        /// entries (array is full) — just the group short @+0, each restored to ITS OWN original group on
        /// despawn. (<paramref name="_unused"/> kept for the call-site signature.)</summary>
        private static void RetagGroupToClone(int _unused, int toGroup)
        {
            RestoreRetag();
            const long tm = 0x21C75870;
            int count = Memory.ReadInt(tm);
            if (count <= 0 || count > TexMgrMax) return;
            int n = 0;
            for (int i = 1; i <= count; i++)
            {
                long e = tm + 0x1148 + (long)(i - 1) * 0x50;
                byte[] nb = Memory.ReadBytesBatch(e + 8, 16);
                if (nb == null) continue;
                int c = 0; while (c < 16 && nb[c] >= 0x20 && nb[c] < 0x7F) c++;
                string name = System.Text.Encoding.ASCII.GetString(nb, 0, c);
                if (!name.StartsWith("c10")) continue;                                   // Ungaga only
                if (!(name.Length > 3 && (name[3] == 'w' || name[3] == 'W'))) continue;  // retag ONLY weapon c10w* (these texture the clone body); keep player BODY c10a* intact
                int orig = (short)(Memory.ReadInt(e) & 0xFFFF);
                Memory.WriteInt(e, (Memory.ReadInt(e) & unchecked((int)0xFFFF0000)) | (toGroup & 0xFFFF));
                _retagEntries.Add(e); _retagOrig.Add(orig);
                Console.WriteLine($"[Mirage/retag] '{name}' 0x{orig:X}→0x{toGroup:X}");
                n++;
            }
            Memory.WriteInt(tm + ((long)toGroup * 0xF + 0x10) * 4, 0);   // group loaded=0 → force reload
            Console.WriteLine($"[Mirage] retagged {n} c10 BODY textures → 0x{toGroup:X} (weapon c10w* kept)");
        }

        private static void RestoreRetag()
        {
            for (int i = 0; i < _retagEntries.Count; i++)
            {
                long e = _retagEntries[i];
                Memory.WriteInt(e, (Memory.ReadInt(e) & unchecked((int)0xFFFF0000)) | (_retagOrig[i] & 0xFFFF));
            }
            _retagEntries.Clear();
            _retagOrig.Clear();
        }

        /// <summary>How many texture-manager entries are tagged with a given group (entry group short @+0;
        /// entries at texMgr+0x1148 stride 0x50; count @texMgr+0).</summary>
        private static int TextureGroupCount(int group)
        {
            const long tm = 0x21C75870;
            int count = Memory.ReadInt(tm);
            if (count <= 0 || count > 8192) return -1;
            int n = 0;
            for (int i = 1; i <= count; i++)
                if ((short)(Memory.ReadInt(tm + 0x1148 + (long)(i - 1) * 0x50) & 0xFFFF) == group) n++;
            return n;
        }

        private static void SetHostOpacity(int slot, float opacity)
        {
            Memory.WriteInt  (EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.OpacityFadeGate), 1);
            Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.OpacityFadeStep), 0f);
            Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Opacity), opacity);
        }

        /// <summary>Stop the hijacked enemy's AI so it can never commit a motion against our foreign clone
        /// tree (the crash source). Clobber its STB label-100 (AI program) with a no-op (push 0; RET) — the
        /// exact mechanism the boss-death system uses to freeze a boss. Original bytes saved for restore.
        /// Data only; the game's own VM runs the no-op (see [[trick-the-game-principle]]).</summary>
        private static void HaltHostAi(int slot)
        {
            _savedLabel100 = null; _savedLabel100Addr = 0;
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0) return;
            long stb = ((long)(uint)stbNative & 0x1FFFFFFF) | 0x20000000;
            if (Memory.ReadInt(stb) != StbVm.Magic) { Console.WriteLine("[Mirage] host STB invalid — AI not halted"); return; }
            int coff = LabelCode(stb, 0x64);   // label-100 = AI program
            if (coff < 0) { Console.WriteLine("[Mirage] host label-100 not found — AI not halted"); return; }
            long l100 = stb + Memory.ReadInt(stb + StbVm.CodeSectionOff) + Memory.ReadInt(stb + coff);
            byte[] hold = StbHoldSeq();
            _savedLabel100 = Memory.ReadBytesBatch(l100, hold.Length);
            _savedLabel100Addr = l100;
            Memory.WriteBytesBatch(l100, hold);
            Console.WriteLine($"[Mirage] halted host AI (label-100 @0x{l100:X})");
        }

        private static void RestoreHostAi()
        {
            if (_savedLabel100 != null && _savedLabel100Addr != 0)
                Memory.WriteBytesBatch(_savedLabel100Addr, _savedLabel100);
            _savedLabel100 = null; _savedLabel100Addr = 0;
        }

        /// <summary>label-table lookup: byte offset of a label's vmcode (relative to the code section).</summary>
        private static int LabelCode(long stb, int label)
        {
            int tblOff = Memory.ReadInt(stb + StbVm.LabelTableOff);
            int cnt    = Memory.ReadInt(stb + StbVm.LabelCount);
            for (int i = 0; i < cnt && i < 64; i++)
            {
                long e = stb + tblOff + i * 8;
                if (Memory.ReadInt(e) == label) return Memory.ReadInt(e + 4);
            }
            return -1;
        }

        /// <summary>STB bytecode: push 0; RET — a no-op program (label returns immediately → AI does nothing).</summary>
        private static byte[] StbHoldSeq()
        {
            var recs = new (uint op, uint opnd, uint val)[] { (3, 1, 0), (0xF, 0, 0) };
            byte[] blk = new byte[recs.Length * 12];
            for (int i = 0; i < recs.Length; i++)
            {
                BitConverter.GetBytes(recs[i].op).CopyTo(blk, i * 12);
                BitConverter.GetBytes(recs[i].opnd).CopyTo(blk, i * 12 + 4);
                BitConverter.GetBytes(recs[i].val).CopyTo(blk, i * 12 + 8);
            }
            return blk;
        }
    }
}
