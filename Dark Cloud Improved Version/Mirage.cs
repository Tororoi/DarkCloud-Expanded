using System;
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
        private const double DecoySeconds = 15.0;
        private const int    FastTickMs   = 25;    // table maintenance cadence while armed + in a dungeon
        private const int    IdleTickMs   = 150;
        private const int    GuardLoopMotion = 9;  // Ungaga's guard-hold loop (spawn here, not on guard-enter)

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

                    if (_armed && inDun)
                    {
                        // Keep un-fooled slots on the live player (the patch reads the table for EVERY
                        // enemy, always) and fooled slots on the decoy — one batched write per fast tick.
                        bool ungagaMirage = Player.Weapon.GetCurrentWeaponId() == Items.mirage &&
                                            Player.CurrentCharacterNum() == Player.UngagaId;

                        if (ungagaMirage && !Player.CheckDunIsPaused())
                        {
                            // Plant the decoy when the player settles into the guard LOOP (motion 9), not the
                            // guard-ENTER transition — so the clone captures and loops the clean guard pose.
                            // Fires on the rising edge of "in guard loop"; refreshes on a new guard.
                            bool guarding = (Memory.ReadUShort(Addresses.buttonInputs) & (ushort)Button.R1) != 0;
                            bool inGuardLoop = guarding &&
                                Memory.ReadInt(MirageClone.PlayerChar + MirageClone.MotionId) == GuardLoopMotion;
                            if (inGuardLoop && !prevGuard) PlaceDecoy();
                            prevGuard = inGuardLoop;
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
        private const bool  EngineDrivenAnim = true;     // let the engine's chara-step pose the clone tree natively (no polling)
        private const bool  IndependentMesh = true;      // copy the software-skinned body meshes → fully independent clone
        private const bool  WeaponEnabled   = true;      // graft the equipped weapon onto the clone's hand bone
        private const bool  SyncMotionToPlayer = false;  // was syncing clone +0xc68 to the player each tick — clamps the clone's animation;
                                                         // OFF = clone free-runs its own motion (test: does decoupling change the flicker?)
        private const float GhostOpacity = 90f;          // of 128; lower = more see-through
        private const float GhostDim     = 1.0f;         // full lighting so any resident character texture shows
        private static int _cloneSlot = -1;   // NPC type slot we borrowed, or -1
        private static uint _srcModelRoot;    // the model root the clone copies/renders (player or test enemy)
        private static uint _cloneRootGuest;  // guest addr of the deep-copied clone root (node pool slot 0)
        private static int  _cloneNodeCount;  // nodes in the last deep copy
        private static uint _weaponRootGuest; // copied weapon tree root (guest); 0 = no weapon grafted
        private static uint _wpnObjPtr;       // the live weapon object (iGpffff9d00) — source of the grip transform
        private static int  _meshesCopied;    // # software-skinned meshes given independent copies this spawn
        private static readonly System.Collections.Generic.List<(long player, long clone)> _chanMap = new();  // motion-channel frame-sync map

        /// <summary>The source model root for the clone: the player's, or (TEST) a live enemy's — to check
        /// whether the NPC slot can render ANY geometry at the decoy.</summary>
        private static uint CloneSourceModelRoot()
        {
            return (uint)Memory.ReadInt(MirageClone.PlayerChar + MirageClone.CharModel);
        }

        private static void SpawnClone()
        {
            if (!CloneEnabled) return;

            // Independent deep copy of the player's whole frame tree.
            _weaponRootGuest = 0; _wpnObjPtr = 0;
            if (!BuildCloneTree()) { _cloneSlot = -1; return; }
            if (IndependentMesh) CopyMeshNodes();   // give the clone its OWN software-skinned body meshes
            if (WeaponEnabled) GraftWeapon();       // graft the equipped weapon onto the clone's hand bone

            // Host = chara slot 0, drawn by the DUNGEON scene draw (Draw__11CSeireiKing) as a single
            // Draw__12CNPCharacter with its own texture group (0x20). Fill the player's draw fields, aim the
            // model at the clone tree, freeze, set position/opacity, mark active, and register it for draw.
            _cloneSlot = 0;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;

            byte[] buf = Memory.ReadBytesBatch(MirageClone.PlayerChar, MirageClone.CharCopySize);
            if (buf == null) { _cloneSlot = -1; return; }
            BitConverter.GetBytes((uint)MirageClone.ClothStubGuest).CopyTo(buf, MirageClone.ClothList);
            BitConverter.GetBytes(GhostSilhouette ? GhostDim : 1.0f).CopyTo(buf, MirageClone.DimFactor);
            // Keep the copied player MotionId (+0xC68 >= 0) so the engine's chara-step (unlocked via the PNACH
            // step-gate NOP) drives SetMotionEX on the clone tree → head/hands animate natively, no polling.
            // NOTE: leave +0xC60 (motion speed) at the copied player value — native speed. A near-zero override
            // made MotionProc loop-to-freeze covering a motion span by 0.001-steps.
            BitConverter.GetBytes(GhostSilhouette ? GhostOpacity : 128f).CopyTo(buf, MirageClone.NpcOpacity);  // +0xcec (Draw needs >0)
            for (int o = MirageClone.LightFrom; o < MirageClone.LightTo; o += 4) BitConverter.GetBytes(0).CopyTo(buf, o);
            // Keep the copied tex-anime object (+0xdc) intact — it selects the face/skin textures; zeroing it
            // left the face dark and arms mis-textured. (Was zeroed for the old shared-texture NPC host.)
            BitConverter.GetBytes(_dx).CopyTo(buf, MirageClone.CharPos);       // +0x10 X
            BitConverter.GetBytes(_dz).CopyTo(buf, MirageClone.CharPos + 4);   // +0x14 Z/height
            BitConverter.GetBytes(_dy).CopyTo(buf, MirageClone.CharPos + 8);   // +0x18 Y
            BitConverter.GetBytes(_cloneRootGuest).CopyTo(buf, MirageClone.CharModel);

            // Give the clone INDEPENDENT motion channels so the engine-step advances its OWN frame counter
            // (not the player's shared one → the 2× fix). Copy each active channel struct into the cave and
            // repoint buf's +0xC20[i]. Internal ptrs (shared keyframe data) copy as-is.
            // When the clone skins its own meshes, it also needs its OWN FRAME_INF bone-matrix buffer (copy
            // once; shared across channels). Otherwise the clone's MotionProc2 clobbers the player's matrices.
            bool ownSkin = IndependentMesh && _meshesCopied > 0;
            uint fiOld = 0, fiNew = 0, bmOld = 0, bmNew = 0;
            int  fiSize = (_cloneNodeCount + 1) * MirageClone.FrameInfEntry;
            int  bmSize = (_cloneNodeCount + 1) * MirageClone.BoneMtxEntry;
            _chanMap.Clear();
            if (EngineDrivenAnim)
            {
                for (int s = 0; s < MirageClone.MotionSlots; s++)
                {
                    int po = MirageClone.MotionSlotBase + s * 4;
                    uint sp = (uint)BitConverter.ToInt32(buf, po) & 0x1FFFFFFF;
                    if (sp == 0 || sp >= 0x02000000) continue;
                    byte[] mstr = Memory.ReadBytesBatch(Memory.ToMmu(sp), MirageClone.MotionStructSize);
                    if (mstr == null) continue;

                    // Give the clone its own FRAME_INF (skinning-matrix buffer), copied once and reused.
                    if (ownSkin)
                    {
                        uint fi = (uint)BitConverter.ToInt32(mstr, MirageClone.FrameInfPtr) & 0x1FFFFFFF;
                        if (fi != 0 && fi < 0x02000000)
                        {
                            if (fiNew == 0 || fi == fiOld)
                            {
                                if (fiNew == 0)
                                {
                                    byte[] fib = Memory.ReadBytesBatch(Memory.ToMmu(fi), fiSize);
                                    if (fib != null) { Memory.WriteBytesBatch(MirageClone.FrameInfCave, fib); fiOld = fi; fiNew = (uint)MirageClone.FrameInfCaveGuest; }
                                }
                                if (fiNew != 0) BitConverter.GetBytes(fiNew).CopyTo(mstr, MirageClone.FrameInfPtr);
                            }
                        }

                        // Give the clone its own per-bone ANIMATION-matrix buffer (channel+0x00), copied once.
                        uint bm = (uint)BitConverter.ToInt32(mstr, MirageClone.BoneMtxPtr) & 0x1FFFFFFF;
                        if (bm != 0 && bm < 0x02000000)
                        {
                            if (bmNew == 0 || bm == bmOld)
                            {
                                if (bmNew == 0)
                                {
                                    byte[] bmb = Memory.ReadBytesBatch(Memory.ToMmu(bm), bmSize);
                                    if (bmb != null) { Memory.WriteBytesBatch(MirageClone.BoneMtxCave, bmb); bmOld = bm; bmNew = (uint)(MirageClone.BoneMtxCave & 0x1FFFFFFF | 0x01000000); }
                                }
                                if (bmNew != 0) BitConverter.GetBytes(bmNew).CopyTo(mstr, MirageClone.BoneMtxPtr);
                            }
                        }
                    }
                    // The SKINNING list (+0x08) drives MotionProc2, which software-skins the mesh vertex buffer.
                    // If the clone has its OWN mesh copies (IndependentMesh), let it skin them → fully independent
                    // body. Otherwise zero the list so it never touches the SHARED mesh (body mirrors the player).
                    // Only let the clone software-skin if it got its OWN mesh copies; else zero the skin list so
                    // it never touches the SHARED mesh (safe fallback = body mirrors, player uncorrupted).
                    if (!IndependentMesh || _meshesCopied == 0) BitConverter.GetBytes(0).CopyTo(mstr, MirageClone.MotionSkinList);
                    long cloneChan = MirageClone.MotionCave + (long)s * MirageClone.MotionStructSize;
                    Memory.WriteBytesBatch(cloneChan, mstr);
                    BitConverter.GetBytes((uint)(MirageClone.MotionCaveGuest + s * MirageClone.MotionStructSize)).CopyTo(buf, po);
                    _chanMap.Add((Memory.ToMmu(sp), cloneChan));   // player channel → clone channel (for frame sync)
                }
            }

            Memory.WriteBytesBatch(MirageClone.ClothStub, new byte[16]);
            Memory.WriteBytesBatch(slot, buf);   // fill the CCharacter fields (0..0xD60)
            Memory.WriteInt(slot + MirageClone.CharaActive, 1);    // active (Draw__12CNPCharacter gate; beyond 0xD60)
            // Engine-driven animation: enable the chara motion step (Step__12CNPCharacter → Step__10CCharacter →
            // SetMotionEX) and zero the opacity ramp so it doesn't fight our GhostOpacity.
            Memory.WriteInt(slot + MirageClone.CharaMotionA, EngineDrivenAnim ? 1 : 0);
            Memory.WriteInt(slot + MirageClone.CharaMotionB, 0);
            Memory.WriteInt(slot + MirageClone.CharaRampA, 0);     // opacity-ramp amount = 0 (no fade drift)
            Memory.WriteInt(slot + MirageClone.CharaRampB, 0);
            Memory.WriteInt(MirageClone.CharaRegistry + (long)_cloneSlot * 4, 1);   // register → dungeon draw draws it
            Console.WriteLine($"[Mirage] clone in chara slot {_cloneSlot} (texgroup 0x{MirageClone.CharaTexBase + _cloneSlot:X}) @({_dx:0.#},{_dz:0.#},{_dy:0.#}) root=0x{_cloneRootGuest:X}");

            if (WeaponEnabled) RegisterWeaponChara();   // clone weapon in its own slot 3 / texgroup 0x1d pass
        }

        /// <summary>Deep-copy the source model's CFrame tree into the cave pool as a CONTIGUOUS 0x270-stride
        /// array preserving the player's memory order. This is REQUIRED for engine posing: MotionProc
        /// (0x147d20) addresses bones as <c>root + boneIndex*0x270</c> — raw array indexing, NOT pointer walks.
        /// So the copy must keep the same stride (0x270) and order; only the link pointers (parent/child/
        /// sibling) are re-based by a single offset (clonePool − playerBase). Geometry (+0x260) stays SHARED.
        /// The result both renders (draw follows the re-based child/sibling) and poses (SetMotionEX indexes the
        /// contiguous array) correctly. Returns false if the tree isn't resolvable or overflows the pool.</summary>
        /// <summary>Graft the equipped weapon onto the clone's hand. The weapon (WeaponObjGlobal → obj, +0xBC =
        /// model root) is a small CFrame tree parented to the player's hand bone but drawn SEPARATELY (not part of
        /// the player's +0xBC tree). We deep-copy that tree into WeaponCave (rebasing its internal links exactly
        /// like BuildCloneTree) and splice the copy's root into the CLONE's hand-bone child list, so the clone's
        /// own MGDraw walks into it and draws the weapon at the clone's hand — no separate draw call needed.
        /// Mesh visuals (node+0x260) are left SHARED: if the weapon is VU1 (rigid) they're per-instance already;
        /// if the weapon flickers it's software-skinned (MDT) and needs the CopyMeshNodes double-buffer copy.</summary>
        private static void GraftWeapon()
        {
            uint wpnObj = (uint)Memory.ReadInt(MirageClone.WeaponObjGlobal) & 0x1FFFFFFF;
            if (wpnObj == 0 || wpnObj >= 0x02000000) { Console.WriteLine("[Mirage/wpn] no weapon object"); return; }
            uint wRoot = (uint)Memory.ReadInt(Memory.ToMmu(wpnObj) + 0xBC) & 0x1FFFFFFF;   // weapon model root
            if (wRoot == 0 || wRoot >= 0x02000000) { Console.WriteLine("[Mirage/wpn] no weapon model"); return; }

            // DFS child/sibling for the tree's contiguous 0x270-stride address span (same layout as the body).
            uint min = wRoot, max = wRoot;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(wRoot);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (n == 0 || n >= 0x02000000 || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > 64) { Console.WriteLine("[Mirage/wpn] weapon tree > 64 nodes — bailing"); return; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }
            if ((max - min) % MirageClone.NodeStride != 0)
            { Console.WriteLine($"[Mirage/wpn] span 0x{max - min:X} not 0x270-aligned — bailing"); return; }
            int nodeCount = (int)((max - min) / MirageClone.NodeStride) + 1;
            int blockSize = nodeCount * MirageClone.NodeStride;
            if (blockSize > MirageClone.WeaponCaveSize)
            { Console.WriteLine($"[Mirage/wpn] weapon tree 0x{blockSize:X} > cave 0x{MirageClone.WeaponCaveSize:X}"); return; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return;

            uint rootOff = wRoot - min;
            uint wCaveG  = (uint)MirageClone.WeaponCaveGuest;
            for (int o = 0; o < blockSize; o += MirageClone.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                WpnRebase(block, o + MirageClone.Parent,      min, max, wCaveG, isRoot);   // root parent → set below
                WpnRebase(block, o + MirageClone.RootChild,   min, max, wCaveG, false);
                WpnRebase(block, o + MirageClone.RootSibling, min, max, wCaveG, isRoot);   // root sibling → set below
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheB);
            }
            Memory.WriteBytesBatch(MirageClone.WeaponCave, block);

            // Parent the copied root to the clone's hand bone (for positioning ONLY — MGDraw seeds a drawn
            // root's world matrix from its parent's cached world, exactly like the player's weapon object). We do
            // NOT insert it into the hand's child list: the weapon draws in its OWN chara slot / texgroup-0x1d
            // pass (RegisterWeaponChara), so the body's 0x11 pass never draws it with the wrong texture.
            uint wRootG = (uint)(wCaveG + rootOff);
            uint handG  = (uint)(MirageClone.NodePoolGuest + MirageClone.WeaponHandBone * MirageClone.NodeStride);
            Memory.WriteUInt(Memory.ToMmu(wRootG) + MirageClone.Parent,      handG);
            Memory.WriteUInt(Memory.ToMmu(wRootG) + MirageClone.RootSibling, 0);   // standalone root
            _weaponRootGuest = wRootG;
            _wpnObjPtr = wpnObj;
            Console.WriteLine($"[Mirage/wpn] weapon tree copied ({nodeCount} nodes) → root 0x{wRootG:X}, parent=clone hand {MirageClone.WeaponHandBone}");
        }

        /// <summary>Register the copied weapon tree as its OWN chara slot (WeaponCharaSlot). Cloned from the body
        /// slot for a valid CCharacter (vtable/light/cloth-stub), then re-aimed: model → weapon tree (parent =
        /// clone hand), transform ← the real weapon object (pos 0,0,0 + grip rot/scale), motion channels zeroed
        /// (rigid). Drawn (registry) but NOT stepped (step-skip table + CharaMotionA=0). The patched group
        /// formula gives this slot texgroup 0x1d, so the weapon samples its own atlas — correct texture.</summary>
        private static void RegisterWeaponChara()
        {
            if (_weaponRootGuest == 0 || _wpnObjPtr == 0 || _cloneSlot < 0) return;
            long src = MirageClone.CharaArray + (long)_cloneSlot        * MirageClone.CharaStride;   // body slot (valid)
            long dst = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
            byte[] cslot = Memory.ReadBytesBatch(src, MirageClone.CharaStride);
            if (cslot == null) return;
            byte[] wtf = Memory.ReadBytesBatch(Memory.ToMmu(_wpnObjPtr) + MirageClone.CharPos, 0x90);   // +0x10..+0xA0 TRS
            if (wtf != null) wtf.CopyTo(cslot, MirageClone.CharPos);
            BitConverter.GetBytes(_weaponRootGuest).CopyTo(cslot, MirageClone.CharModel);               // +0xbc = weapon tree
            for (int s = 0; s < MirageClone.MotionSlots; s++)                                            // rigid: no motion
                BitConverter.GetBytes(0).CopyTo(cslot, MirageClone.MotionSlotBase + s * 4);
            Memory.WriteBytesBatch(dst, cslot);
            Memory.WriteInt  (dst + MirageClone.CharaActive, 1);                                         // +0x146c draw gate
            Memory.WriteFloat(dst + MirageClone.NpcOpacity, GhostSilhouette ? GhostOpacity : 128f);      // +0xcec > 0
            Memory.WriteInt  (dst + MirageClone.CharaMotionA, 0);                                        // never step
            Memory.WriteInt  (dst + MirageClone.CharaMotionB, 0);
            Memory.WriteInt  (MirageClone.StepSkipTable  + (long)MirageClone.WeaponCharaSlot * 4, 1);    // step loop skips it
            Memory.WriteInt  (MirageClone.CharaRegistry  + (long)MirageClone.WeaponCharaSlot * 4, 1);    // draw loop draws it
            Console.WriteLine($"[Mirage/wpn] weapon chara slot {MirageClone.WeaponCharaSlot} (texgroup 0x1d) model=0x{_weaponRootGuest:X}");
        }

        /// <summary>Re-base a weapon-tree link into WeaponCave (mirror of Rebase but for the weapon cave); zero
        /// external refs and, when forceZero, the root's parent/sibling (set explicitly by the graft splice).</summary>
        private static void WpnRebase(byte[] block, int off, uint min, uint max, uint caveGuest, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & 0x1FFFFFFF;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max) neu = (uint)(caveGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        private static bool BuildCloneTree()
        {
            _srcModelRoot = CloneSourceModelRoot();
            uint rootPhys = _srcModelRoot & 0x1FFFFFFF;
            if (_srcModelRoot == 0 || rootPhys == 0 || rootPhys >= 0x02000000) return false;

            // DFS over child/sibling to find the tree's address SPAN (the bones form a contiguous 0x270 array
            // rooted at the model root, so min == rootPhys and the block is [min, max+0x270)).
            uint min = rootPhys, max = rootPhys;
            var seen = new System.Collections.Generic.HashSet<uint>();
            var work = new System.Collections.Generic.Stack<uint>();
            work.Push(rootPhys);
            while (work.Count > 0)
            {
                uint n = work.Pop();
                if (n == 0 || n >= 0x02000000 || !seen.Add(n)) continue;
                if (n < min) min = n; if (n > max) max = n;
                if (seen.Count > MirageClone.MaxNodes)
                { Console.WriteLine($"[Mirage] clone tree > {MirageClone.MaxNodes} nodes — aborting"); return false; }
                for (uint c = (uint)Memory.ReadInt(Memory.ToMmu(n) + MirageClone.RootChild) & 0x1FFFFFFF;
                     c != 0 && c < 0x02000000;
                     c = (uint)Memory.ReadInt(Memory.ToMmu(c) + MirageClone.RootSibling) & 0x1FFFFFFF)
                    work.Push(c);
            }

            int nodeCount = (int)((max - min) / MirageClone.NodeStride) + 1;
            int blockSize = nodeCount * MirageClone.NodeStride;
            if ((max - min) % MirageClone.NodeStride != 0)
            { Console.WriteLine($"[Mirage] tree span 0x{max - min:X} not 0x270-aligned — bailing"); return false; }

            byte[] block = Memory.ReadBytesBatch(Memory.ToMmu(min), blockSize);
            if (block == null) return false;

            // Re-base every link pointer by (clonePool − playerBase); zero external refs and the root's
            // parent/sibling; invalidate world caches. Same-offset translation keeps array indexing valid.
            uint rootOff = rootPhys - min;
            for (int o = 0; o < blockSize; o += MirageClone.NodeStride)
            {
                bool isRoot = (uint)o == rootOff;
                Rebase(block, o + MirageClone.Parent,      min, max, isRoot);   // root's parent → 0
                Rebase(block, o + MirageClone.RootChild,   min, max, false);
                Rebase(block, o + MirageClone.RootSibling, min, max, isRoot);   // root's sibling → 0
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheA);
                BitConverter.GetBytes(0).CopyTo(block, o + MirageClone.WorldCacheB);
            }
            Memory.WriteBytesBatch(MirageClone.NodePool, block);

            _cloneNodeCount = nodeCount;
            _cloneRootGuest = (uint)(MirageClone.NodePoolGuest + rootOff);   // clone root = pool + root offset
            Console.WriteLine($"[Mirage] contiguous {nodeCount}-node clone tree (0x270 stride) → root 0x{_cloneRootGuest:X}");
            return true;
        }

        /// <summary>Re-base a node link pointer: if it targets a node inside the copied block [min, max+0x270),
        /// rewrite it to the clone pool at the same offset; otherwise (external ref, or forceZero for the
        /// root's parent/sibling) zero it.</summary>
        private static void Rebase(byte[] block, int off, uint min, uint max, bool forceZero)
        {
            uint old = (uint)BitConverter.ToInt32(block, off) & 0x1FFFFFFF;
            uint neu = 0;
            if (!forceZero && old >= min && old <= max)
                neu = (uint)(MirageClone.NodePoolGuest + (old - min));
            BitConverter.GetBytes(neu).CopyTo(block, off);
        }

        /// <summary>Give the clone its own copy of every SOFTWARE-SKINNED mesh (CVisualMDTVu1). Those are the
        /// only bones whose vertex buffer MotionProc2 deforms in place; copying them (visual + VU data + MDT)
        /// and repointing node+0x260 lets the clone skin its OWN buffers → a body independent of the player.
        /// The other bones skin on VU1 at draw time and are already per-instance. Cross-references between the
        /// three copied blocks are rebased by scanning for words in the old address ranges (safe: model data
        /// pointers are low 0x00xxxxxx addresses, distinct from vertex floats).</summary>
        private static void CopyMeshNodes()
        {
            long cave = MirageClone.MeshCave, caveGuest = MirageClone.MeshCaveGuest;
            long caveEnd = MirageClone.MeshCave + MirageClone.MeshCaveSize;
            int copied = 0;
            for (int i = 0; i < _cloneNodeCount; i++)
            {
                long node = MirageClone.NodePool + (long)i * MirageClone.NodeStride;
                uint vis = (uint)Memory.ReadInt(node + MirageClone.GeomPtr) & 0x1FFFFFFF;
                if (vis == 0 || vis >= 0x02000000) continue;
                uint mdt = (uint)Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisMDT) & 0x1FFFFFFF;
                uint magic = (mdt != 0 && mdt < 0x02000000) ? (uint)Memory.ReadInt(Memory.ToMmu(mdt)) : 0xDEAD;
                if (mdt == 0 || mdt >= 0x02000000) continue;
                if (magic != MirageClone.MdtMagic) continue;   // not software-skinned

                uint vu    = (uint)Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisVU) & 0x1FFFFFFF;
                int  vuSz  = Memory.ReadInt(Memory.ToMmu(vis) + MirageClone.VisVU + 4) * 16;   // field is in QWORDS
                int  mdtSz = Memory.ReadInt(Memory.ToMmu(mdt) + MirageClone.MdtSizeField);
                if (vu == 0 || vuSz <= 0 || vuSz > 0x40000 || mdtSz <= 0 || mdtSz > 0x40000) continue;

                int visSz = MirageClone.VisualSize;
                int need  = Align16(visSz) + Align16(vuSz) + Align16(mdtSz);
                if (cave + need > caveEnd) { Console.WriteLine($"[Mirage/mesh] cave full at n{i}"); break; }

                long cVis = cave;              uint cVisG = (uint)caveGuest;
                long cVU  = cave + Align16(visSz); uint cVUG = (uint)(caveGuest + Align16(visSz));
                long cMDT = cVU + Align16(vuSz);   uint cMDTG = (uint)(caveGuest + Align16(visSz) + Align16(vuSz));

                byte[] visB = Memory.ReadBytesBatch(Memory.ToMmu(vis), visSz);
                byte[] vuB  = Memory.ReadBytesBatch(Memory.ToMmu(vu),  vuSz);
                byte[] mdtB = Memory.ReadBytesBatch(Memory.ToMmu(mdt), mdtSz);
                if (visB == null || vuB == null || mdtB == null) continue;

                // The visual ALWAYS repoints to the clone copies (+0x18 → clone VU, +0x20 → clone MDT).
                RebaseRange(visB, vu, vuSz, cVUG);
                RebaseRange(visB, mdt, mdtSz, cMDTG);
                foreach (byte[] b in new[] { vuB, mdtB })
                {
                    RebaseRange(b, vu,  vuSz,  cVUG);
                    RebaseRange(b, mdt, mdtSz, cMDTG);
                }

                // Force BOTH double-buffer slots (+0x28/+0x2c) and the current ptr (+0x18) to the clone's single
                // VU copy. n66 is truly double-buffered (two distinct buffers, DBuffID-toggled); copying only one
                // left +0x2c aimed at the PLAYER's buffer, so on DBuffID=1 frames the clone wrote the player's
                // buffer → flicker. Single-buffering the clone (like n64/n65) can't corrupt the player.
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x18);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x28);
                BitConverter.GetBytes(cVUG).CopyTo(visB, 0x2c);

                Memory.WriteBytesBatch(cVU,  vuB);
                Memory.WriteBytesBatch(cMDT, mdtB);
                Memory.WriteBytesBatch(cVis, visB);
                Memory.WriteUInt(node + MirageClone.GeomPtr, cVisG);   // node draws the clone's own mesh
                cave += need; caveGuest += need; copied++;
            }
            _meshesCopied = copied;
        }

        private static int Align16(int n) => (n + 15) & ~15;

        /// <summary>Rewrite every 4-byte word in <paramref name="b"/> whose value points into [oldBase,
        /// oldBase+size) to the corresponding offset in the clone copy at newBaseGuest.</summary>
        private static void RebaseRange(byte[] b, uint oldBase, int size, uint newBaseGuest)
        {
            for (int o = 0; o + 4 <= b.Length; o += 4)
            {
                uint w = (uint)BitConverter.ToInt32(b, o);
                uint phys = w & 0x1FFFFFFF;
                if (phys >= oldBase && phys < oldBase + (uint)size)
                    BitConverter.GetBytes(newBaseGuest + (phys - oldBase)).CopyTo(b, o);
            }
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

            // Engine-driven animation: the engine's own chara-step (unlocked via the PNACH step-gate NOP) poses
            // the clone tree at 60fps — no polling. Re-assert the step flags (game may reset between our ticks)
            // and mirror the player's current MotionId so the clone plays whatever the player is doing. The
            // near-zero motion speed keeps it posing at the player's shared frame without double-advancing it.
            if (EngineDrivenAnim)
            {
                Memory.WriteInt  (slot + MirageClone.CharaMotionA, 1);
                Memory.WriteInt  (slot + MirageClone.CharaRampA,   0);
                if (SyncMotionToPlayer)
                {
                    int pmid = Memory.ReadInt(MirageClone.PlayerChar + MirageClone.MotionId);
                    if (pmid >= 0) Memory.WriteInt(slot + MirageClone.MotionId, pmid);   // mirror the player's action (clamps clone)
                }
            }
            // (Legacy per-tick TRS poll disabled: it fought the 60fps step and read as jitter.)

            // Re-assert the weapon chara slot (drawn, never stepped) — the game may reset registry/opacity.
            if (_weaponRootGuest != 0)
            {
                long wslot = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
                Memory.WriteUInt (wslot + MirageClone.CharModel,   _weaponRootGuest);
                Memory.WriteInt  (wslot + MirageClone.CharaActive, 1);
                Memory.WriteFloat(wslot + MirageClone.NpcOpacity,  GhostSilhouette ? GhostOpacity : 128f);
                Memory.WriteInt  (wslot + MirageClone.CharaMotionA, 0);
                Memory.WriteInt  (MirageClone.StepSkipTable + (long)MirageClone.WeaponCharaSlot * 4, 1);
                Memory.WriteInt  (MirageClone.CharaRegistry + (long)MirageClone.WeaponCharaSlot * 4, 1);
            }

            // NOTE: iGpffff9e18 (0x202A3608) is the "scene active" flag — setting it draws the chara loop BUT
            // freezes enemies (motionDrive steps enemies only when it's 0). So we DON'T set it here; the clone
            // is drawn instead via an in-place patch of Draw__11CSeireiKing's chara-loop gate (SceneDrawPatch),
            // which lets the loop run while the flag stays 0 so enemies keep moving.
        }

        private static void DespawnClone()
        {
            Memory.WriteInt(SceneGateFlag, 0);   // disarm the PNACH scene-gate NOP (always — keeps town safe)
            if (_cloneSlot < 0) return;
            long slot = MirageClone.CharaArray + (long)_cloneSlot * MirageClone.CharaStride;
            Memory.WriteInt (MirageClone.CharaRegistry + (long)_cloneSlot * 4, 0);   // unregister
            Memory.WriteInt (slot + MirageClone.CharaActive, 0);                     // inactive
            Memory.WriteUInt(slot + MirageClone.CharModel, 0);                       // clear model

            if (_weaponRootGuest != 0)   // tear down the weapon chara slot too
            {
                long wslot = MirageClone.CharaArray + (long)MirageClone.WeaponCharaSlot * MirageClone.CharaStride;
                Memory.WriteInt (MirageClone.CharaRegistry + (long)MirageClone.WeaponCharaSlot * 4, 0);
                Memory.WriteInt (MirageClone.StepSkipTable + (long)MirageClone.WeaponCharaSlot * 4, 0);
                Memory.WriteInt (wslot + MirageClone.CharaActive, 0);
                Memory.WriteUInt(wslot + MirageClone.CharModel, 0);
                _weaponRootGuest = 0;
            }
            _cloneSlot = -1;
        }

        private const int TexMgrMax = 0xC4;   // 196 CTexture slots (Initialize__15CTextureManager inits 0xC4) — HARD cap

    }
}
