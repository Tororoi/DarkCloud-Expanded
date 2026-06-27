using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY reach test: extends <b>Heaven's Cloud</b> (item 271) to ~3x reach by scaling its
    /// loaded weapon model's <c>dcol*</c> damage-collision frame translations in RAM.
    ///
    /// The equipped-model pointer (<see cref="WeaponCollision.EquippedModelPtr"/>) is the CHARACTER
    /// model root; the <c>dcol*</c> frames live in the weapon subtree attached under a hand bone, so
    /// we WALK the CFrame tree (child <see cref="WeaponCollision.CFrameChild"/> / next
    /// <see cref="WeaponCollision.CFrameNext"/>, name <see cref="WeaponCollision.CFrameName"/>) to
    /// find them, then multiply each frame's local-matrix translation (+0x180/+0x184/+0x188) by
    /// <see cref="Multiplier"/>. Scaled once per loaded model instance;
    /// <see cref="OnFloorEntered"/> re-arms it per floor. Remove when reach work is finalized.
    /// </summary>
    public static class WeaponSpawner
    {
        const int   HeavensCloudId = 271;
        const float Multiplier     = 10.0f;
        const int   MaxNodes       = 8192; // tree-walk safety cap

        static bool   _started;
        static int    _scaledModel; // native ptr of the model we last scaled (0 = re-arm)
        static string _lastState = "";

        public static void StartReachExtender()
        {
            if (_started) return;
            _started = true;
            new Thread(Loop) { IsBackground = true }.Start();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "WeaponSpawner: Heaven's Cloud reach extender started (tree walk)");
        }

        /// <summary>Call on each floor entry so the freshly reloaded model gets re-scaled.</summary>
        public static void OnFloorEntered() => _scaledModel = 0;

        static void Loop()
        {
            while (true)
            {
                try { Tick(); }
                catch (Exception ex)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "WeaponSpawner ERROR: " + ex.Message);
                }
                Thread.Sleep(400);
            }
        }

        static void Tick()
        {
            int chr = Player.CurrentCharacterNum();
            ushort wid = Player.Weapon.GetCurrentWeaponId();
            int rootPtr = Memory.ReadInt(WeaponCollision.EquippedModelPtr);

            string state = $"char={chr} wid={wid}({Item.GetName(wid)}) rootCFrame=0x{rootPtr:X8}";
            if (state != _lastState)
            {
                _lastState = state;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[REACH] " + state);
            }

            if (chr != Player.ToanId || wid != HeavensCloudId || rootPtr == 0) return;

            // Re-apply every poll (idempotent: only scales when a frame has reverted to base).
            // If the re-scale counters keep climbing, the engine re-poses the frame every frame.
            ScaleDcolFrames(rootPtr);
        }

        static int _dcolRescales, _weaponRescales;
        static bool _loggedNoDcol, _dumpedFields;

        // DIAGNOSTIC: dump a CFrame's fields +0x150..+0x250 as float+hex to locate the bind/base
        // matrix the per-frame pose copies into +0x1d0 (look for a 2nd copy of (2.68,0,4) / 4.0).
        static void DumpFields(long b, string name)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"  FIELD DUMP {name} @0x{b:X8}:");
            for (int off = 0x150; off < 0x250; off += 4)
            {
                uint raw = (uint)Memory.ReadInt(b + off);
                float f = Memory.ReadFloat(b + off);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"    +0x{off:X3}  0x{raw:X8}  {f:0.####}");
            }
        }

        /// <summary>Walks the CFrame tree from the character root, scaling every dcol* frame found.</summary>
        static int ScaleDcolFrames(int rootNative)
        {
            long rootBase = Memory.ToMmu(rootNative);
            int firstChild = Memory.ReadInt(rootBase + WeaponCollision.CFrameChild);

            var stack = new Stack<int>();
            var seen  = new HashSet<int>();
            if (firstChild != 0) stack.Push(firstChild);

            int scaled = 0, visited = 0;
            var names = new List<string>();

            while (stack.Count > 0 && visited < MaxNodes)
            {
                int node = stack.Pop();
                if (node == 0 || !seen.Add(node)) continue;
                visited++;

                long b = Memory.ToMmu(node);
                string name = Memory.ReadString(b + WeaponCollision.CFrameName, 16);
                int term = name.IndexOf('\0');
                if (term >= 0) name = name.Substring(0, term);
                if (names.Count < 60 && name.Length > 0) names.Add(name);

                if (name.StartsWith("dcol", StringComparison.Ordinal))
                {
                    if (!_dumpedFields) { _dumpedFields = true; DumpFields(b, name); }
                    scaled++;
                }

                int child = Memory.ReadInt(b + WeaponCollision.CFrameChild);
                int next  = Memory.ReadInt(b + WeaponCollision.CFrameNext);
                if (child != 0) stack.Push(child);
                if (next  != 0) stack.Push(next);
            }

            if (scaled == 0 && !_loggedNoDcol)
            {
                _loggedNoDcol = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"WeaponSpawner: no dcol in {visited} frames. names: " + string.Join(",", names));
            }
            return scaled;
        }

        static float ScaleAxis(long addr)
        {
            float v = Memory.ReadFloat(addr) * Multiplier;
            Memory.WriteFloat(addr, v);
            return v;
        }

        // DIAGNOSTIC: dumps a frame and its whole subtree (child/next), logging each frame's
        // local-matrix scale diagonal + translation and world translation, so we can identify the
        // weapon's blade / mesh frames to scale for "C".
        static void DumpSubtree(int rootNative)
        {
            DumpFrame(rootNative, 0);
            var st = new Stack<(int node, int depth)>();
            var seen = new HashSet<int>();
            int firstChild = Memory.ReadInt(Memory.ToMmu(rootNative) + WeaponCollision.CFrameChild);
            if (firstChild != 0) st.Push((firstChild, 1));

            int n = 0;
            while (st.Count > 0 && n < 150)
            {
                var (node, depth) = st.Pop();
                if (node == 0 || !seen.Add(node)) continue;
                n++;
                DumpFrame(node, depth);
                long b = Memory.ToMmu(node);
                int child = Memory.ReadInt(b + WeaponCollision.CFrameChild);
                int next  = Memory.ReadInt(b + WeaponCollision.CFrameNext);
                if (child != 0) st.Push((child, depth + 1));
                if (next  != 0) st.Push((next, depth));
            }
        }

        static void DumpFrame(int node, int depth)
        {
            long b = Memory.ToMmu(node);
            string nm = Memory.ReadString(b + WeaponCollision.CFrameName, 16);
            int t = nm.IndexOf('\0'); if (t >= 0) nm = nm.Substring(0, t);

            long lm = b + WeaponCollision.CFrameLocalMatrix; // +0x1d0
            float lx = Memory.ReadFloat(lm + 0x30), ly = Memory.ReadFloat(lm + 0x34), lz = Memory.ReadFloat(lm + 0x38);
            float s0 = Memory.ReadFloat(lm + 0x00), s1 = Memory.ReadFloat(lm + 0x14), s2 = Memory.ReadFloat(lm + 0x28);
            long wm = b + WeaponCollision.CFrameWorldMatrix; // +0x150
            float wx = Memory.ReadFloat(wm + 0x30), wy = Memory.ReadFloat(wm + 0x34), wz = Memory.ReadFloat(wm + 0x38);

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"    {new string(' ', depth * 2)}{nm} @0x{b:X8} " +
                $"scaleDiag=({s0:0.##},{s1:0.##},{s2:0.##}) localT=({lx:0.##},{ly:0.##},{lz:0.##}) " +
                $"worldT=({wx:0.##},{wy:0.##},{wz:0.##})");
        }
    }
}
