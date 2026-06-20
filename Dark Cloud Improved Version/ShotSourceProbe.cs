using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// READ-ONLY live diagnostic for PROJECTILE-damage normalization (RE'd 2026-06-19).
    ///
    /// The shot-damage cache was located by disassembling _SET_SHOT (STB cmd 134, ELF 0x1E4120) and _SET_SHOT2
    /// (cmd 136, 0x1E4310): each writes a per-slot struct at MMU + slot*0x30 + 0x5FF50 (SHOT) / +0x60250 (SHOT2),
    /// with the damage int at struct+0x28 (see <see cref="ShotDmgCache"/>). CMonstorUnit::Step (0x1DD540) reads it
    /// each frame and fires only when damage != −1.
    ///
    /// This probe confirms those offsets in-game and resolves the "default shooter" question (do Sam=58 /
    /// Crescent Baron=70 carry their damage in this field, or is it −1 and sourced elsewhere?). It logs, per slot,
    /// each time SHOT/SHOT2 damage transitions to a NEW non-(−1) value (i.e. when the enemy fires), plus the live
    /// STB base from CRunScript.StbPtr(slot) and its magic, so the script's shot-damage constant can be located.
    ///
    /// Read-only — it never writes RAM. Disable once the projectile normalizer's STB-constant patch is built.
    /// </summary>
    internal static class ShotSourceProbe
    {
        internal static bool Enabled = true;   // read-only diagnostic; safe to leave on while validating

        private const int StbMagic = 0x00425453; // "STB\0"

        // Per-slot last-logged damage, so we only emit on change (avoids spamming the −1 idle value).
        private static readonly Dictionary<int, int> _lastShot  = new();
        private static readonly Dictionary<int, int> _lastShot2 = new();
        // STB bases already analyzed (header dump + constant-locate), so we do it once each.
        private static readonly HashSet<int> _analyzedStb = new();
        // Slots whose BST shot-damage source has been logged (once each).
        private static readonly HashSet<int> _bstProbed = new();

        // Static ELF tables (RAM = native | 0x20000000): the "default" shot-damage source.
        private const long SpeciesTableBase = 0x2027FB00; // EnemySpeciesTable record 0
        private const int  SpeciesStride    = 0x9C;
        private const int  SpeciesCount     = 200;        // 162 real + slack
        private const int  SpeciesIdField   = 0x7C;       // ushort EnemySpeciesId in the record
        private const int  ShotIdx0Field    = 0x68;       // ushort shot-effect index (primary)
        private const int  ShotIdx1Field    = 0x6A;       // ushort shot-effect index (secondary)
        private const long BstPtrArray      = 0x2027FA70; // pointer array → BST entries
        private const int  BstDamageField   = 0x3C;       // BST_entry+0x3C = shot base damage

        internal static void Tick()
        {
            if (!Enabled) return;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long slotB = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slotB + EnemySlotOffsets.RenderStatus) < 0) continue;
                int id = Memory.ReadUShort(slotB + EnemySlotOffsets.EnemySpeciesId);
                if (id == 0) continue;   // empty / not-yet-populated slot

                if (_bstProbed.Add(s)) ProbeBstDamage(s, id);   // verify the static BST default-shot source live

                int shot  = Memory.ReadInt(ShotDmgCache.ShotDamageAddr(s));
                int shot2 = Memory.ReadInt(ShotDmgCache.Shot2DamageAddr(s));

                // Only act on a real fired value: 0 = uninitialized (before first shot), −1 = "no explicit damage".
                if (shot > 0 && (!_lastShot.TryGetValue(s, out int ls) || ls != shot))
                {
                    _lastShot[s] = shot;
                    Console.WriteLine($"[ShotProbe] slot {s} id {id}: SHOT damage = {shot} @0x{ShotDmgCache.ShotDamageAddr(s):X8}{StbInfo(s)}");
                    AnalyzeStb(s, shot);   // locate this damage constant inside the loaded STB
                }
                if (shot2 > 0 && (!_lastShot2.TryGetValue(s, out int ls2) || ls2 != shot2))
                {
                    _lastShot2[s] = shot2;
                    Console.WriteLine($"[ShotProbe] slot {s} id {id}: SHOT2 damage = {shot2} @0x{ShotDmgCache.Shot2DamageAddr(s):X8}{StbInfo(s)}");
                    AnalyzeStb(s, shot2);
                }
            }
        }

        // Resolve a present enemy's "default" shot damage from the static ELF tables and log it, to verify the
        // chain found offline (species record +0x68/+0x6A → BstPtrArray[idx] → BST entry +0x3C). Expect e.g.
        // Sam(id 85)=58, Crescent Baron(id 76)=70.
        private static void ProbeBstDamage(int slot, int id)
        {
            for (int ti = 0; ti < SpeciesCount; ti++)
            {
                long rec = SpeciesTableBase + (long)ti * SpeciesStride;
                if (Memory.ReadUShort(rec + SpeciesIdField) != id) continue;
                var sb = new System.Text.StringBuilder();
                foreach (var (tag, fld) in new[] { ("shot0", ShotIdx0Field), ("shot1", ShotIdx1Field) })
                {
                    int idx = Memory.ReadUShort(rec + fld);
                    if (idx == 0xFFFF) continue;
                    int bt = Memory.ReadInt(BstPtrArray + idx * 4);
                    if (bt == 0) continue;
                    long btRam = ((long)bt & 0x1FFFFFFF) | 0x20000000;
                    sb.Append($" {tag}=idx{idx}->bst0x{bt:X}+0x3C={Memory.ReadInt(btRam + BstDamageField)}");
                }
                Console.WriteLine($"[ShotProbe] slot {slot} id {id} TI {ti} default-shot(BST):{sb}");
                return;
            }
            Console.WriteLine($"[ShotProbe] slot {slot} id {id}: no species record found");
        }

        private static long StbAddr(int slot)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return 0;
            return (stbNative & 0x1FFFFFFF) | 0x20000000;
        }

        // The live STB base for this slot (CRunScript+0x3C) + magic validation, to locate the shot-damage constant.
        private static string StbInfo(int slot)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (stbNative == 0 || stbNative == -1) return "  (stb: none)";
            long stb = (stbNative & 0x1FFFFFFF) | 0x20000000;
            int magic = Memory.ReadInt(stb);
            string ok = magic == StbMagic ? "STB" : $"?0x{magic:X8}";
            return $"  (stb native 0x{stbNative:X8} magic {ok})";
        }

        // One-time per STB: dump the RS_PROG_HEADER words and report every 4-byte offset where the damage value
        // appears, so the shot-damage constant (the patch target for projectile normalization) can be pinned.
        private static void AnalyzeStb(int slot, int dmg)
        {
            long stb = StbAddr(slot);
            if (stb == 0) return;
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(slot));
            if (!_analyzedStb.Add(stbNative)) return;
            if (Memory.ReadInt(stb) != StbMagic) return;

            // RS_PROG_HEADER first 24 words — section offsets/counts live here (e.g. code @+0x08, labels @+0x0C/+0x10).
            var hdr = new System.Text.StringBuilder();
            for (int i = 0; i < 24; i++) hdr.Append($" +0x{i*4:X2}={Memory.ReadInt(stb + i * 4)}");
            Console.WriteLine($"[ShotProbe]   STB 0x{stbNative:X8} header:{hdr}");

            // Scan a bounded window for the damage value, as raw int and as an 8-byte {tag,value} pool entry.
            const int Window = 0x4000;
            byte[] d = Memory.ReadByteArray(stb, Window);
            if (d == null) return;
            var raw = new List<int>();
            for (int i = 0; i + 4 <= d.Length; i += 4)
            {
                int v = d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
                if (v == dmg) raw.Add(i);
            }
            Console.WriteLine($"[ShotProbe]   STB 0x{stbNative:X8} value {dmg}: {raw.Count} hit(s) @[{string.Join(",", raw.ConvertAll(x => "0x" + x.ToString("X")))}]");

            // For each hit, dump the surrounding words so the vmcode records (12-byte {op,A,B}) around the value can
            // be read — this reveals the op3-literal push and the cmd-134 ext-call structure to build the patcher.
            int Word(int i) => (i >= 0 && i + 4 <= d.Length) ? (d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24)) : 0;
            foreach (int hit in raw)
            {
                var w = new System.Text.StringBuilder();
                for (int j = hit - 0x24; j <= hit + 0x14; j += 4)
                    w.Append(j == hit ? $" [{Word(j)}]" : $" {Word(j)}");
                Console.WriteLine($"[ShotProbe]     @0x{hit:X} ctx words[-9..+5]:{w}");
            }
        }
    }
}
