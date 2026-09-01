using System;
using static Dark_Cloud_Improved_Version.CustomFishingSpot;
using static Dark_Cloud_Improved_Version.FishingLabelIds;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Where the fishing scripts LIVE in a town's loaded event.stb: claim the ISO-baked, pre-numbered labels
    /// (the normal path) or fall back to hijacking never-dispatched native spare labels (the arena), then
    /// serialize StbWriter scripts into them (code + string blob + jump fixups + the label header fields).
    /// </summary>
    internal static class FishingLabelAllocator
    {
        /// <summary>
        /// Labels that must NOT be hijacked.
        ///
        /// The cutoff was 200, and that let label **256** through — which in Yellow Drops is the TOWN'S OWN
        /// script (3196 bytes, by far the biggest). Overwriting it left the screen black on load. 256 is
        /// only the fishing script in NORUNE; elsewhere it is the town's main event, and its size is exactly
        /// what made "pick the biggest region" choose it.
        ///
        /// The 300+ block is per-event scripting and is what we have been safely overwriting all along
        /// (310, 305, 304). Everything below it is either an engine handler or the town itself.
        /// </summary>
        /// <summary>
        /// Label ids we may hijack for the fishing scripts, in every town. Derived by an OFFLINE scan of all
        /// 33 town event.stb files in the (immutable) vanilla ISO: these are the ONLY 300-block labels never
        /// dispatched by any town's script (via _NEXT_EVENT/_FADEOUT_TO_EVENT) — i.e. dead placeholder slots
        /// everywhere. Notably 300 is EXCLUDED: it is a real, dispatched event in Queens (e03) and Yellow
        /// Drops (s13), so hijacking it — as the old size-first picker did — silently broke a town event.
        /// A fixed whitelist beats a runtime scan here: the data can't change, a scan that found everything
        /// used would leave the spot uninstallable, and one list keeps every area consistent. (BuildHijackPool
        /// still drops any of these a live event POINT references — cheap insurance for a future area — but
        /// no town's event points touch the 300-block, verified live for the three fishing towns.)
        /// </summary>
        // 301-310: the towns' own native spare labels (offline-verified never dispatched). This pool is the
        // FALLBACK path only — a spot on an unpatched disc, or a town without baked labels. The three custom
        // fishing towns are ISO-patched with labels 9600/400/133/134 already numbered (StbLabelBaker.ExtendStb),
        // which the installer claims directly by id (ClaimLabel/FindLabelById), bypassing this pool entirely.
        private static readonly System.Collections.Generic.HashSet<int> SafeHijackLabels =
            new System.Collections.Generic.HashSet<int>
            { 301, 302, 303, 304, 305, 306, 307, 310 };

        /// <summary>One hijackable label: its table slot, its id, and the code region it owns.</summary>
        internal sealed class ScriptLabel
        {
            internal int Slot, Id, Off, Size, Entry;
            internal bool Used;
        }

        internal static readonly System.Collections.Generic.List<ScriptLabel> _hijackPool =
            new System.Collections.Generic.List<ScriptLabel>();

        /// <summary>
        /// Collect the hijackable labels, in CODE ORDER.
        ///
        /// Label code regions tile the buffer end to end — each label's code runs until the next label's
        /// <c>codeOffset</c>. So a run of ADJACENT spare labels is one contiguous span we can write straight
        /// through, which is the only way the ~2 KB entry script fits: the spare labels in Yellow Drops are
        /// 650-800 B apiece.
        /// </summary>
        internal static void BuildHijackPool(long stb, int labelCount, int tbl)
        {
            _hijackPool.Clear();

            // PROTECT LABELS A LIVE EVENT POINT DISPATCHES. We only ever hijack labels the town isn't using —
            // system labels (<300) are protected by id, but a town CAN put a real story trigger on a >=300
            // label. So also protect any label an active type-3 event point references (ItemOrLabel). This is
            // the guarantee that installing a fishing spot never silently breaks a story/quest trigger; without
            // it, Allocate could retire a label something in the world still fires.
            var referenced = new System.Collections.Generic.HashSet<int>();
            long arr = EventPoints.Base();
            int eventPointCount = arr == 0 ? 0 : Memory.ReadInt(EventPoints.Count);
            const int MaxEventPoints = 0x100;   // the event array physically holds 256 points
            for (int i = 0; i < eventPointCount && i <= MaxEventPoints; i++)
            {
                long e = EventPoints.Slot(arr, i);
                if (Memory.ReadInt(e + EventPoints.Type) == EventPoints.TypeScript)
                    referenced.Add(Memory.ReadInt(e + EventPoints.ItemOrLabel));
            }
            referenced.Remove(FishingLabelId);     // our own point, from a prior install — not a town event

            var all = new System.Collections.Generic.List<(int id, int off, int slot)>();
            for (int i = 0; i < labelCount; i++)
            {
                long e = stb + tbl + i * TownScript.LabelStride;
                all.Add((Memory.ReadInt(e), Memory.ReadInt(e + 4), i));
            }
            all.Sort((a, b) => a.off.CompareTo(b.off));

            // Candidates come from the fixed SafeHijackLabels whitelist (offline-verified never dispatched in
            // ANY town). No runtime bytecode scan: the vanilla data can't change, and a scan that found
            // everything used would leave the spot uninstallable. The event-point set above is still consulted
            // as cheap insurance (a future area could put a point on one), though none does today.
            var sizes = new System.Text.StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                int size = i + 1 < all.Count ? all[i + 1].off - all[i].off : 0;   // 0 = last, unknown end
                bool safe = SafeHijackLabels.Contains(all[i].id);
                bool epRef = referenced.Contains(all[i].id);
                sizes.Append($"{all[i].id}:{(size > 0 ? size.ToString() : "end")}" +
                             $"{(safe ? "+" : "")}{(epRef ? "@" : "")} ");
                if (!safe || epRef || size <= 0) continue;   // + = safe hijack pool, @ = event-point (skip)
                _hijackPool.Add(new ScriptLabel
                {
                    Slot = all[i].slot, Id = all[i].id, Off = all[i].off, Size = size,
                    Entry = (int)(tbl + all[i].slot * TownScript.LabelStride),
                });
            }
            Log($"   label regions (+ = safe hijack pool, @ = event-point protected): {sizes}");
        }

        /// <summary>Bytes a script needs: header skip + code + string blob + alignment slack.</summary>
        internal static int ScriptByteSize(StbWriter w) => TownScript.LabelCodeSkip + w.ToArray().Length + w.StringBytes + 8;

        /// <summary>
        /// Claim a run of adjacent unused labels totalling at least <paramref name="need"/> bytes, and return
        /// the FIRST one — its id is what the script will answer to.
        ///
        /// FEWEST LABELS FIRST. Every extra label a run swallows is a town event we destroy, so try to fit in
        /// one label before considering two, and so on. Taking the first run that merely fits would grab a
        /// 644+644 pair when a single 804 was sitting right there — and would then retire a label for nothing.
        ///
        /// Every label a run does swallow is marked used (so a later allocation cannot hand out the same
        /// bytes) and RETIRED (so the engine cannot dispatch into the middle of the script we write over it).
        /// </summary>
        internal static ScriptLabel Allocate(long stb, int need, out int end)
        {
            for (int len = 1; len <= _hijackPool.Count; len++)
            for (int i = 0; i + len <= _hijackPool.Count; i++)
            {
                int total = 0;
                bool usable = true;
                for (int j = i; j < i + len; j++)
                {
                    if (_hijackPool[j].Used ||
                        (j > i && _hijackPool[j].Off != _hijackPool[j - 1].Off + _hijackPool[j - 1].Size))   // not adjacent
                    { usable = false; break; }
                    total += _hijackPool[j].Size;
                }
                if (!usable || total < need) continue;

                {
                    int j = i + len - 1;
                    for (int k = i; k <= j; k++) _hijackPool[k].Used = true;

                    // RETIRE THE SWALLOWED LABELS. A run's later labels keep their table entries, but we are
                    // about to write straight THROUGH their code — so their codeOffset would then point into
                    // the middle of our bytecode. If the town ever asks for one (an event that fires when you
                    // reach some part of the map, say), the VM reads our data as a funcdata, takes a garbage
                    // code offset from it, and jumps into nowhere. That is the crash-on-walking-away.
                    //
                    // Give them an id nothing will ever request. The engine then simply fails to find the
                    // label and treats it as a no-op event, which loses whatever that event did — but a lost
                    // town event beats a hard crash, and there is nowhere else to put a 1.5 KB script.
                    for (int k = i + 1; k <= j; k++)
                    {
                        Memory.WriteInt(stb + _hijackPool[k].Entry, RetiredLabelId + k);
                        Log($"   label {_hijackPool[k].Id} RETIRED (its code is inside our script now) — " +
                            $"the town can no longer dispatch into it");
                    }

                    end = _hijackPool[i].Off + total;
                    return _hijackPool[i];
                }
            }
            end = 0;
            return null;
        }

        /// <summary>
        /// The label to write <paramref name="targetId"/>'s script into. PREFERS the ISO-baked label that
        /// already carries this id (<see cref="StbLabelBaker.ExtendStb"/> stamps 9600/400/133/134 straight into
        /// the three custom fishing towns): it is correctly numbered and sized to hold its one script, so we
        /// write into it directly — no renumber, no arena run, no spanning. FALLS BACK to renaming a native
        /// orphan for a town/ISO without the baked labels (an unpatched disc, or a spot added to a new town).
        /// </summary>
        internal static ScriptLabel ClaimLabel(long stb, int labelCount, int tbl, int targetId, int need, out int end)
        {
            ScriptLabel baked = FindLabelById(stb, labelCount, tbl, targetId);
            if (baked != null) { end = baked.Off + baked.Size; return baked; }
            return Allocate(stb, need, out end);   // native-orphan fallback; caller renames it to targetId
        }

        /// <summary>Find the label whose id is <paramref name="id"/> and return its code region (its size is
        /// the gap to the next label by offset). Null if absent. Used to claim a pre-baked, pre-numbered
        /// fishing label directly, without the fit-allocator's size search.</summary>
        internal static ScriptLabel FindLabelById(long stb, int labelCount, int tbl, int id)
        {
            int myOff = -1, mySlot = -1;
            var offs = new int[labelCount];
            for (int i = 0; i < labelCount; i++)
            {
                long e = stb + tbl + i * TownScript.LabelStride;
                offs[i] = Memory.ReadInt(e + 4);
                if (Memory.ReadInt(e) == id) { myOff = offs[i]; mySlot = i; }
            }
            if (mySlot < 0) return null;
            int next = int.MaxValue;
            for (int i = 0; i < labelCount; i++) if (offs[i] > myOff && offs[i] < next) next = offs[i];
            return new ScriptLabel
            {
                Id = id, Slot = mySlot, Off = myOff,
                Size = next == int.MaxValue ? 0 : next - myOff,
                Entry = tbl + mySlot * TownScript.LabelStride,
            };
        }

        /// <summary>
        /// Serialize a script at <paramref name="codeOff"/>, placing any strings it pushed just past its code.
        /// String operands are offsets from the script's CODE BASE, so the blob must live inside the buffer.
        /// </summary>
        internal static void WriteScript(long stb, int codeOff, int end, StbWriter w, string what)
        {
            int codeBase = Memory.ReadInt(stb + TownScript.CodeBase);
            int scriptOff = codeOff + TownScript.LabelCodeSkip;

            byte[] bc = w.ToArray();
            int blobOff = (scriptOff + bc.Length + 3) & ~3;
            byte[] blob = w.EmitStrings(blobOff, codeBase);
            w.EmitJumps(scriptOff, codeBase);       // jump targets are codeBase-relative, like strings
            bc = w.ToArray();                       // re-read: both passes patched the operands in place

            int last = blobOff + blob.Length;
            if (last > end)
            {
                Log($"   REFUSING to write: needs +0x{codeOff:X}..+0x{last:X}, arena ends at +0x{end:X}");
                return;
            }

            // Declare our locals. A label's header starts with the LOCAL VARIABLE COUNT. The labels we hijack
            // declare 0, so a script that touches var0 without raising this would be reaching outside its
            // frame. (header layout + Norune's per-label counts: memory stb-label-header-format.md,
            // game_data/docs/fishing-engine-re.md §stb-label-header)
            if (w.Locals > 0) Memory.WriteInt(stb + codeOff + 8, w.Locals);
            // fd[3] (funcOff+0xC) = argument count. Native/baked spares carry 0 here, so only a genuine
            // subroutine (the shared menu) needs it — but a wrong non-zero value would misframe the callee.
            if (w.ArgCount > 0) Memory.WriteInt(stb + codeOff + 0xC, w.ArgCount);

            Memory.WriteBytesBatch(stb + codeOff + TownScript.LabelCodeSkip, bc);
            if (blob.Length > 0) Memory.WriteBytesBatch(stb + blobOff, blob);
            Log($"   wrote {bc.Length}B code + {blob.Length}B strings @+0x{blobOff:X}" +
                (w.Locals > 0 ? $", {w.Locals} local(s)" : "") + $": {what}");
        }

        /// <summary>
        /// Give the town a label the ENGINE asks for by number (133 = quit, 134 = bait). The id is not
        /// negotiable, so if the town has no such label we claim a spare and REWRITE ITS ID.
        /// </summary>
        internal static void InstallEngineLabel(long stb, int labelCount, int tbl, int targetId, StbWriter w, string what)
        {
            ScriptLabel lab = ClaimLabel(stb, labelCount, tbl, targetId, ScriptByteSize(w), out int end);
            if (lab == null)
            {
                Log($"   NO room for label {targetId} — that fishing button will do nothing");
                return;
            }

            Memory.WriteInt(stb + lab.Entry, targetId);   // no-op for a baked label; renames a fallback orphan
            Log($"   label {targetId} (the engine requests it by number, code @+0x{lab.Off:X})");
            WriteScript(stb, lab.Off, end, w, what);
        }
    }
}
