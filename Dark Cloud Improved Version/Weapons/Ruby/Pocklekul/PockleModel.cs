using System;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Pocklekul's pockle as a model: one of the seven day-of-the-week pockles loaded at run time into Ruby's pockle block
    /// (<see cref="PockleBlock"/>) and stood in a dungeon character slot (<see cref="DungeonCharaDraw"/>, slot
    /// <see cref="PockleBlock.CharaSlot"/>), which the engine draws and steps once the scene gate is open — no enemy slot.
    ///
    /// The load is the engine's own, as a dungeon event loads its characters: the day's pack is read from the disc image
    /// (<see cref="GameDataFiles"/>) into the floor loader's read buffer, then through <see cref="NativeCall"/>
    /// CCharacter::Initialize, InitializeTexAnime (records in the block's head) and LoadPackData3 into the slot with the block's own
    /// allocator and the slot's texture group; it copies everything it keeps (model, motions, texture bank) into the block, so the
    /// read buffer is free again at once. Its texture group gets a VRAM window of its own first: every group's range starts at the
    /// same address and is re-uploaded right before its draw, which the slot's draw outruns (the cat's lesson, CatTextures), so the
    /// group's record is pointed below the fixed textures' cursor before the load (LoadPackData3 keeps a record; the group is
    /// deleted by hand first), the model's packets are built with those addresses, and the cursor is moved under the window after. Then, as CDungeonMap::SetNPC does for a map's characters, fog on (SetAttr) and each
    /// node's render flags from its name (SetFrameAttr). It stands with the scene gate at <see cref="Mailbox.SceneGatePockle"/>, which
    /// also opens the shadow pass's NPC loop: it casts its own shadow model, posed by ShadowStep. While it stands the gate is the
    /// pockle's (Mirage's loop, the gate's usual driver, stands down on <see cref="Up"/>: a competing write blinks the draw).
    /// The textures are watched: a menu that clears the group gets them entered again from
    /// the copy (images[0]). The block goes with the next LoadChara2 and an event may load its own character into the slot, so the
    /// pockle is forgotten (never written back) on a floor change, a party switch or an event.</summary>
    internal static class PockleModel
    {
        private const string Tag = "[Pocklekul/PockleModel] ";
        internal static readonly (string code, string day)[] Days =
        {
            ("e14a", "Sunday"), ("e15a", "Monday"), ("e16a", "Tuesday"), ("e17a", "Wednesday"),
            ("e18a", "Thursday"), ("e19a", "Friday"), ("e20a", "Saturday"),
        };
        private const float Opacity = 128f;
        private const int   IdleMotion = 0;
        private const int   LoadTimeoutMs = 3000;

        private static int  _day = -1;             // the day standing in the slot, or -1
        private static long _block;                // the block it was loaded into (MMU)
        private static int  _floorKey = -1;        // the floor it was loaded on
        private static int  _texCount = -1;        // its texture entries last seen
        private static int  _effectPeak;           // the effect counter's high-water mark since the load (units)
        private static uint _window, _windowEnd;   // its texture group's VRAM window (blocks of 256 B), 0 = none
        private static uint _cursorSaved;          // the fixed textures' cursor before the window was taken
        private const int   WindowSlack = 0x20;    // blocks past the pictures' own, before the alignment

        internal static bool Up => _day >= 0;
        internal static int  Day => _day;
        internal static long Slot => DungeonCharaDraw.CharaArray + (long)PockleBlock.CharaSlot * DungeonCharaDraw.CharaStride;
        private static uint SlotGuest => (uint)(Slot & Memory.PhysAddrMask);

        /// <summary>The pockle block (MMU) while Ruby's character pool holds it, else 0: another character is loaded, or the disc's
        /// dun.bin lacks the reservation (the block would then reach below her c05_f03.chr copy).</summary>
        internal static long BlockAddr()
        {
            if (Player.CurrentCharacterNum() != Player.RubyId) return 0;
            uint charaBase = Memory.ReadGuestPtr(DataPools.Chara);
            int  used      = Memory.ReadInt(DataPools.Chara + DataPools.Used);
            uint end       = charaBase + (uint)used * 16, start = end - (uint)PockleBlock.Units * 16;
            uint effect    = Memory.ReadGuestPtr(PockleBlock.RubyEffectCopyPtr);
            if (!Memory.IsValidGuest(charaBase) || Memory.ReadGuestPtr(DataPools.Weapon) != end || effect == 0 || start <= effect) return 0;
            return Memory.ToMmu(start);
        }

        /// <summary>Load <paramref name="day"/>'s pockle into the block and the slot (not shown yet). The caller holds the floor quiet.</summary>
        internal static bool Load(int day)
        {
            long block = BlockAddr();
            if (block == 0) { Log("no pockle block (Ruby's character pool lacks it — a disc patched before the Pocklekul?)"); return false; }
            string code = Days[day].code;
            byte[] chr = GameDataFiles.TryReadEntry($@"dun\monstor\{code}.chr");
            if (chr == null) { Log($"{code}.chr not readable"); return false; }
            uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
            if (!Memory.IsValidGuest(buf)) { Log("no read buffer"); return false; }

            int pictures = TextureBlocks(chr);
            if (pictures <= 0) { Log($"{code}: no picture in its pack"); return false; }
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor), highest = HighestBlockEnd();
            uint window = (cursor - (uint)(pictures + WindowSlack)) & ~0x1Fu;
            if (cursor == 0 || window <= highest) { Log($"no VRAM window for its textures: cursor 0x{cursor:X}, the highest group ends at 0x{highest:X}, {pictures} blocks wanted"); return false; }

            uint g = (uint)(block & Memory.PhysAddrMask);
            var head = new byte[PockleBlock.Data];
            BitConverter.GetBytes(g + PockleBlock.Data).CopyTo(head, PockleBlock.Alloc);          // base
            BitConverter.GetBytes(g + PockleBlock.Data).CopyTo(head, PockleBlock.Alloc + 4);      // buffer
            BitConverter.GetBytes(0).CopyTo(head, PockleBlock.Alloc + 8);                         // used
            BitConverter.GetBytes(PockleBlock.Units - PockleBlock.Data / 16).CopyTo(head, PockleBlock.Alloc + 0xC);   // limit
            Encoding.ASCII.GetBytes("info.cfg").CopyTo(head, PockleBlock.CfgName);
            Memory.WriteBytesBatch(block, head);
            Memory.WriteBytesBatch(Memory.ToMmu(buf), chr);

            Forget();
            if (!NativeCall.Invoke(CharacterLoad.DeleteTextureBlock, out _, CharacterLoad.TexManagerGuest, PockleBlock.TexBlock)) { Log("the texture group was not cleared (no dungeon frame took the call)"); return false; }
            PresetWindow(window);
            if (!NativeCall.Invoke(CharacterLoad.Initialize, out _, SlotGuest)
                || !NativeCall.Invoke(CharacterLoad.InitializeTexAnime, out _, SlotGuest, g + PockleBlock.TexAnime, PockleBlock.TexAnimeCount)
                || !NativeCall.Invoke(CharacterLoad.LoadPackData3, out _, SlotGuest, buf, g + PockleBlock.CfgName, g + PockleBlock.Alloc,
                                      PockleBlock.TexBlock, g + PockleBlock.Alloc, timeoutMs: LoadTimeoutMs, a6: 1, a7: 0))
            { Log($"{code}: the load was not made (no dungeon frame took the call)"); return false; }
            _window = window; _windowEnd = Memory.ReadUInt(GroupRecord + TextureManager.BlkTop); _cursorSaved = cursor;
            Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, window);                // the window reserved: nothing fixed lands in it
            Log($"{code}: textures in VRAM 0x{window:X}..0x{_windowEnd:X} (cursor 0x{cursor:X} → 0x{window:X}; the highest group ended at 0x{highest:X})");

            uint frame = Memory.ReadGuestPtr(Slot + CCharacter.CharModel);
            int units = Memory.ReadInt(block + PockleBlock.Alloc + 8);
            if (!Memory.IsValidGuest(frame)) { Log($"{code}: loaded no model ({units} units used)"); return false; }
            var attr = new byte[CharacterLoad.AttrSize];                                          // the read buffer is free again: the attribute there
            attr[CharacterLoad.AttrFogEnable] = 1;
            Memory.WriteBytesBatch(Memory.ToMmu(buf), attr);
            if (!NativeCall.Invoke(CharacterLoad.SetAttr, out _, frame, buf, 1, CharacterLoad.AttrMaskFog)
                || !NativeCall.Invoke(CharacterLoad.SetFrameAttr, out _, frame, 1))
                Log($"{code}: its frame attributes were not set (no dungeon frame took the call)");
            Memory.WriteInt(Slot + DungeonCharaDraw.CharaTexBlock, PockleBlock.TexBlock);
            _day = day; _block = block; _floorKey = FloorKey(); _texCount = -1; _effectPeak = 0; _scaleNoted = false;
            if (_windowEnd > cursor) Log($"WARNING: its textures reach 0x{_windowEnd:X}, past the cursor they were given (0x{cursor:X})");
            Log($"{Days[day].day}'s pockle ({code}) loaded: {units + PockleBlock.Data / 16:N0} of {PockleBlock.Units:N0} units, model 0x{frame:X}; " + PoolLine());
            return true;
        }

        /// <summary>Stand the loaded pockle at (x, h, y) facing <paramref name="yaw"/>, idle, and open the scene gate.</summary>
        internal static void Show(float x, float h, float y, float yaw)
        {
            if (!Up) return;
            long s = Slot;
            Memory.WriteVec3(s + CCharacter.CharPos, x, h, y);
            Memory.WriteFloat(s + CCharacter.CharPos + 0xC, 1f);
            Memory.WriteVec3(s + CCharacter.CharRot, 0f, yaw, 0f);
            Memory.WriteVec3(s + CCharacter.CharaTint, 0f, 0f, 0f);
            Memory.WriteBytesBatch(s + CCharacter.LightFrom, new byte[CCharacter.LightTo - CCharacter.LightFrom]);
            Memory.WriteFloat(s + CCharacter.NpcOpacity, Opacity);
            Memory.WriteFloat(s + CharacterMotion.MotionSpeedOffset, CharacterMotion.MotionSpeedUseKey);
            Memory.WriteInt(s + CCharacter.MotionId, IdleMotion);
            Memory.WriteUInt(s + CCharacter.MotionFlags, (uint)CCharacter.MotionRestart);
            Memory.WriteInt(s + DungeonCharaDraw.CharaActive, 1);
            Memory.WriteInt(s + DungeonCharaDraw.CharaMotionA, 1);
            Memory.WriteInt(s + DungeonCharaDraw.CharaMotionB, 0);
            Memory.WriteInt(s + DungeonCharaDraw.CharaRampA, 0);
            Memory.WriteInt(s + DungeonCharaDraw.CharaRampB, 0);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + PockleBlock.CharaSlot * 4, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + PockleBlock.CharaSlot * 4, 1);
            Memory.WriteInt(DungeonCharaDraw.ShadowRegistry + PockleBlock.CharaSlot * 4, 1);
            Memory.WriteInt(Mailbox.MirageSceneGate, Mailbox.SceneGatePockle);
            Log($"{Days[_day].day}'s pockle stands at ({x:0.#}, {h:0.#}, {y:0.#}) in slot {PockleBlock.CharaSlot} (texture group 0x{PockleBlock.TexBlock:X})");
        }

        /// <summary>Upkeep each tick while it stands: forgotten on a floor change, a party switch or an event; drawn but frozen while
        /// paused; its textures entered again after a menu cleared them. <paramref name="quiet"/>: the floor allows a native call.</summary>
        internal static void Tick(bool quiet)
        {
            if (!Up) return;
            if (FloorKey() != _floorKey || BlockAddr() != _block) { Log("its floor or Ruby's pool changed — forgotten"); Forget(); return; }
            if (Memory.ReadInt(DungeonScriptEvent.BtEventMode) != 0) { Log("an event took the floor — forgotten"); Unshow(); Forget(); return; }
            Memory.WriteInt(Mailbox.MirageSceneGate, Player.CheckDunIsPaused() ? Mailbox.SceneGatePocklePaused : Mailbox.SceneGatePockle);
            int effect = Memory.ReadInt(DataPools.Effect + DataPools.Used);
            if (effect > _effectPeak) { _effectPeak = effect; Log("effects grew: " + PoolLine()); }

            int tex = TextureEntries();
            if (tex != _texCount) { Log($"texture group 0x{PockleBlock.TexBlock:X}: {tex} entries"); _texCount = tex; }
            if (tex == 0 && quiet) EnterTextures();
            KeepScale();
        }

        private const int FrameScale = 0x210, FrameLocal = 0x1D0;     // CFrame: its scale (Draw sets it from the slot's), its local matrix
        private static bool _scaleNoted;
        /// <summary>The slot's scale held at 1 (Draw copies it onto the model every frame). A pockle once drew stretched sideways: anything
        /// found off — the slot's scale, the model root's, or a root local matrix whose rows are not unit length — is logged once per
        /// pockle with its values, and the scales put back.</summary>
        private static void KeepScale()
        {
            long s = Slot;
            float sx = Memory.ReadFloat(s + CCharacter.CharScale), sy = Memory.ReadFloat(s + CCharacter.CharScale + 4), sz = Memory.ReadFloat(s + CCharacter.CharScale + 8);
            uint root = Memory.ReadGuestPtr(s + CCharacter.CharModel);
            float fx = 1f, fy = 1f, fz = 1f; string rows = "";
            if (Memory.IsValidGuest(root))
            {
                long r = Memory.ToMmu(root);
                fx = Memory.ReadFloat(r + FrameScale); fy = Memory.ReadFloat(r + FrameScale + 4); fz = Memory.ReadFloat(r + FrameScale + 8);
                for (int row = 0; row < 3; row++)
                {
                    float a = Memory.ReadFloat(r + FrameLocal + row * 16), b = Memory.ReadFloat(r + FrameLocal + row * 16 + 4), c = Memory.ReadFloat(r + FrameLocal + row * 16 + 8);
                    float len = MathF.Sqrt(a * a + b * b + c * c);
                    if (MathF.Abs(len - 1f) > 0.05f) rows += $" row{row} {len:0.###}";
                }
            }
            bool off(float v) => MathF.Abs(v - 1f) > 0.01f;
            if (!off(sx) && !off(sy) && !off(sz) && !off(fx) && !off(fy) && !off(fz) && rows.Length == 0) return;
            if (!_scaleNoted)
            {
                _scaleNoted = true;
                Log($"{Days[_day].day}'s pockle off scale: slot ({sx:0.###}, {sy:0.###}, {sz:0.###}), root ({fx:0.###}, {fy:0.###}, {fz:0.###})"
                    + (rows.Length > 0 ? $", root local{rows}" : "") + " — scales put back to 1");
            }
            if (off(sx) || off(sy) || off(sz)) Memory.WriteVec3(s + CCharacter.CharScale, 1f, 1f, 1f);
        }

        // ───────────────────────────── the pockle as a character ─────────────────────────────
        /// <summary>The standing day's species record (its HP, melee damage and defence are the pockle's).</summary>
        internal static EnemyDefaults Species => DaySpecies[Math.Max(_day, 0)];
        private static readonly EnemyDefaults[] DaySpecies =
            { EnemySpecies.Sunday, EnemySpecies.Monday, EnemySpecies.Tuesday, EnemySpecies.Wednesday, EnemySpecies.Thursday, EnemySpecies.Friday, EnemySpecies.Saturday };
        /// <summary>Its position's guest address: a 16-byte-aligned vec4 the redirected scripts can read as their "player".</summary>
        internal static uint PosGuest => SlotGuest + (uint)CCharacter.CharPos;
        internal static (float x, float h, float y) Position
            => (Memory.ReadFloat(Slot + CCharacter.CharPos), Memory.ReadFloat(Slot + CCharacter.CharPos + 4), Memory.ReadFloat(Slot + CCharacter.CharPos + 8));
        internal static float Yaw => Memory.ReadFloat(Slot + CCharacter.CharRotY);
        internal static float MotionFrame => Memory.ReadFloat(Slot + CCharacter.MotionFrame);
        internal static int Motion => Memory.ReadInt(Slot + CCharacter.MotionId);

        internal static void Place(float x, float h, float y, float yaw)
        {
            Memory.WriteVec3(Slot + CCharacter.CharPos, x, h, y);
            Memory.WriteFloat(Slot + CCharacter.CharRotY, yaw);
        }
        internal static void SetYaw(float yaw) => Memory.WriteFloat(Slot + CCharacter.CharRotY, yaw);
        internal static void SetHeight(float h) => Memory.WriteFloat(Slot + CCharacter.CharPos + 4, h);
        /// <summary>The playing clip's rate (an absolute frames-a-step override; −1 = the clip's own).</summary>
        internal static void SetRate(float rate) => Memory.WriteFloat(Slot + CharacterMotion.MotionSpeedOffset, rate);

        /// <summary>The pockle's step a frame for the next <paramref name="frames"/> dungeon frames (tools/stubs/pockle_move.s moves it): the
        /// step first, the frames last; 0 frames = still.</summary>
        internal static void Step(float stepX, float stepY, int frames)
        {
            Memory.WriteFloat(CodeCaves.PockleMove + CodeCaves.PockleMoveX, stepX);
            Memory.WriteFloat(CodeCaves.PockleMove + CodeCaves.PockleMoveY, stepY);
            Memory.WriteInt(CodeCaves.PockleMove + CodeCaves.PockleMoveFrames, frames);
        }

        /// <summary>The clip restarted from its first frame, once and held (a cycle of a repeated attack).</summary>
        internal static void RestartClip(int id)
        {
            Memory.WriteInt(Slot + CCharacter.MotionId, id);
            Memory.WriteUInt(Slot + CCharacter.MotionFlags, (uint)(CCharacter.MotionRestart | CCharacter.MotionPlayOnce));
        }

        /// <summary>Clip <paramref name="id"/> played from its first frame (a blend from the pose it leaves), looping, or once and held
        /// on its last frame; the same clip again is left running.</summary>
        internal static void SetMotion(int id, bool once = false)
        {
            if (Motion == id) return;
            Memory.WriteInt(Slot + CCharacter.MotionId, id);
            Memory.WriteUInt(Slot + CCharacter.MotionFlags, (uint)(CCharacter.MotionRestart | (once ? CCharacter.MotionPlayOnce : 0)));
        }

        /// <summary>Clip <paramref name="id"/>'s first and last frames from the model's motion list.</summary>
        internal static (float start, float end) ClipRange(int id)
        {
            uint list = Memory.ReadGuestPtr(Slot + CCharacter.MotionList);
            if (!Memory.IsValidGuest(list)) return (0f, 0f);
            long e = Memory.ToMmu(list) + (long)id * CCharacter.MotionEntryStride;
            return (Memory.ReadInt(e + CCharacter.MotionEntryStart), Memory.ReadInt(e + CCharacter.MotionEntryEnd));
        }

        /// <summary>Take it out of the draw and forget it.</summary>
        internal static void Despawn(string why)
        {
            if (!Up) return;
            Unshow();
            Log($"{Days[_day].day}'s pockle gone ({why})");
            Forget();
        }

        private static void Unshow()
        {
            Step(0f, 0f, 0);
            Memory.WriteInt(DungeonCharaDraw.CharaRegistry + PockleBlock.CharaSlot * 4, 0);
            Memory.WriteInt(DungeonCharaDraw.ShadowRegistry + PockleBlock.CharaSlot * 4, 0);
            Memory.WriteInt(DungeonCharaDraw.StepSkipTable + PockleBlock.CharaSlot * 4, 1);
            Memory.WriteInt(Slot + DungeonCharaDraw.CharaActive, 0);
            // Mirage's loop drives the gate again once Up is false; off a floor the town's 0 at once (the PNACH keeps applying a mode
            // every frame, and the town overlay lives at the same addresses)
            Memory.WriteInt(Mailbox.MirageSceneGate, Player.InDungeonFloor() ? Mailbox.SceneGateVanilla : 0);
        }

        private static void Forget()
        {
            if (_window != 0 && Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor) == _window)
                Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _cursorSaved);          // the window given back
            _day = -1; _block = 0; _floorKey = -1; _texCount = -1; _window = 0; _windowEnd = 0; _cursorSaved = 0;
        }

        private static long GroupRecord => TextureManager.Base + TextureManager.Blocks + (long)PockleBlock.TexBlock * TextureManager.BlockStride;

        /// <summary>The group's record aimed at the window, empty: what the load (or a re-entry) fills from there.</summary>
        private static void PresetWindow(uint window)
        {
            long r = GroupRecord;
            Memory.WriteUInt(r + TextureManager.BlkBase, window);
            Memory.WriteUInt(r + TextureManager.BlkTop, window);
            Memory.WriteUInt(r + TextureManager.BlkLoaded, 0);
            Memory.WriteUInt(r + TextureManager.BlkDirty, 0);
        }

        /// <summary>Where the highest texture group (other than the pockle's) ends in VRAM.</summary>
        private static uint HighestBlockEnd()
        {
            uint highest = 0;
            for (int b = 0; b < TextureManager.BlockCount; b++)
                if (b != PockleBlock.TexBlock)
                    highest = Math.Max(highest, Memory.ReadUInt(TextureManager.Base + TextureManager.Blocks + (long)b * TextureManager.BlockStride + TextureManager.BlkTop));
            return highest;
        }

        /// <summary>The VRAM blocks (256 B) the pack's pictures take as the engine counts them (EnterTextureEX: width × height × bytes
        /// per pixel / 256, + 4 for an 8-bit picture's palette), or 0 with no bank.</summary>
        private static int TextureBlocks(byte[] chr)
        {
            int blocks = 0;
            foreach (var rec in ChrPack.Parse(chr).Records)
            {
                if (!rec.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) continue;
                var bank = new ImgBank(rec.Payload);
                foreach (var (name, _) in bank.Entries)
                {
                    byte[] tim = bank.Block(name);
                    if (tim.Length < 0x30 || tim[0] != 'T' || tim[1] != 'I' || tim[2] != 'M' || tim[3] != '2') continue;
                    int type = tim[0x23], w = BitConverter.ToUInt16(tim, 0x24), h = BitConverter.ToUInt16(tim, 0x26);
                    int bits = type switch { 1 => 16, 2 => 24, 3 => 32, 4 => 4, 5 => 8, _ => 32 };
                    blocks += (w * h * bits / 8) >> 8;
                    if (type == 5 || type == 4) blocks += 4;
                }
            }
            return blocks;
        }

        /// <summary>The pockle's texture bank entered again into its group from its copy in the block (images[0]), into the same
        /// window (the model's packets name those addresses), and the cursor put back under it if a rebuild raised it — AFTER the
        /// entry: EndEnterTextureBlock spins forever ("fix texture over") on a group ending above the cursor. Skipped when the
        /// rebuilt manager has handed the window to something else.</summary>
        private static void EnterTextures()
        {
            uint img = Memory.ReadGuestPtr(Slot + CharacterLoad.Images);
            if (!Memory.IsValidGuest(img) || _window == 0) return;
            uint g = (uint)(_block & Memory.PhysAddrMask);
            var info = new byte[0x18];
            BitConverter.GetBytes(img).CopyTo(info, 0);
            BitConverter.GetBytes(PockleBlock.TexBlock).CopyTo(info, 4);
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor), highest = HighestBlockEnd();
            if (cursor < _windowEnd || highest > _window)
            { Log($"textures not entered again: the window 0x{_window:X}..0x{_windowEnd:X} is taken (cursor 0x{cursor:X}, highest group 0x{highest:X})"); _window = 0; return; }
            Memory.WriteBytesBatch(_block + PockleBlock.TexInfo, info);
            PresetWindow(_window);
            if (!NativeCall.Invoke(CharacterLoad.LoadTextureBlockEX, out _, CharacterLoad.TexManagerGuest, PockleBlock.TexBlock, g + PockleBlock.TexInfo)) return;
            if (cursor > _window) { _cursorSaved = cursor; Memory.WriteUInt(TextureManager.Base + TextureManager.Cursor, _window); }
            uint end = Memory.ReadUInt(GroupRecord + TextureManager.BlkTop);
            Log($"textures entered again into group 0x{PockleBlock.TexBlock:X}: VRAM 0x{_window:X}..0x{end:X}" + (end != _windowEnd ? $" (WARNING: the load's ended at 0x{_windowEnd:X})" : ""));
        }

        private static int TextureEntries()
        {
            int n = 0;
            for (int i = 1; i < TextureManager.MaxEntries; i++)
                if (Memory.ReadUShort(TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride + TextureManager.EntryBlock) == PockleBlock.TexBlock) n++;
            return n;
        }

        private static int FloorKey()
            => (Memory.ReadByte(Addresses.checkDungeon) << 16) | (Memory.ReadByte(Addresses.checkFloor) << 8) | (Memory.ReadByte(Addresses.dunBackFloorFlag) != 0 ? 1 : 0);

        /// <summary>Ruby's character pool as the counters stand (units): her character data (with the block), weapons, effects.</summary>
        private static string PoolLine()
        {
            int chara = Memory.ReadInt(DataPools.Chara + DataPools.Used), cap = Memory.ReadInt(DataPools.Chara + DataPools.Cap);
            int weapon = Memory.ReadInt(DataPools.Weapon + DataPools.Used), effect = Memory.ReadInt(DataPools.Effect + DataPools.Used);
            int effectCap = Memory.ReadInt(DataPools.Effect + DataPools.Cap);
            return $"pool chara {chara:N0} (block {PockleBlock.Units:N0}) + weapons {weapon:N0} + effects {effect:N0}/{effectCap:N0} of {cap:N0} units";
        }

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
