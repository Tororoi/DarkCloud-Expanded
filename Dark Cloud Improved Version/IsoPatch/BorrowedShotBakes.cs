using System;
using System.Collections.Generic;
using System.IO;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Effect containers made loadable through the dungeon's shot-effect pack under the `dun\effect` names no
    /// species uses. The pack's loader (Entry__12CSHOT_EFFECT) takes ONE name from a BT_SHOT_EFFECT config and uses it
    /// twice: `dun/effect/&lt;name&gt;.chr` is the archive file, `&lt;name&gt;.cfg` the record it then asks that container
    /// for — a container whose cfg record is named differently loads nothing. So each source container is copied onto
    /// the dead name with one record appended, `&lt;name&gt;.cfg` = the source's cfg payload, nothing else changed. The mod
    /// fires these with BorrowedShots.CustomConfig.</summary>
    internal static class BorrowedShotBakes
    {
        // dead `dun\effect` name → (source container, the record to expose as <name>.cfg)
        private static readonly (string name, string source, string cfg)[] Borrowed =
        {
            ("_b_boll",   @"dun\effect\_b_boll.chr",   "b_boll.cfg"),
            ("_f_boll",   @"dun\effect\_f_boll.chr",   "f_boll.cfg"),
            ("_f_boll_2", @"dun\effect\_f_boll_2.chr", "f_boll_2.cfg"),
            ("_f_boll_3", @"dun\effect\_f_boll_3.chr", "f_boll_3.cfg"),
            ("_i_boll",   @"dun\effect\_i_boll.chr",   "i_boll.cfg"),
            ("es_zone",   @"dun\effect\es_zone.chr",   "info.cfg"),
        };

        /// <summary>The container with `&lt;name&gt;.cfg` appended (the payload of <paramref name="cfgRecord"/>), or null
        /// when it is there already.</summary>
        internal static byte[] Expose(byte[] container, string name, string cfgRecord)
        {
            var pack = ChrPack.Parse(container);
            if (pack.Find(name + ".cfg") != null) return null;
            var src = pack.Find(cfgRecord) ?? throw new IOException($"{name}: no record {cfgRecord} in the source container");
            byte[] payload = src.Payload;
            pack.Records.Add(ChrRecord.Create(name + ".cfg", payload));
            byte[] outp = pack.Rebuild();
            var check = ChrPack.Parse(outp);
            if (check.Records.Count != pack.Records.Count || !check.Require(name + ".cfg").Payload.AsSpan().SequenceEqual(payload))
                throw new IOException($"{name}: the rebuilt container does not read back");
            return outp;
        }

        internal static void Run(IsoArchive arc, Action<string> log)
        {
            int done = 0;
            foreach (var (name, source, cfg) in Borrowed)
            {
                string target = $@"dun\effect\{name}.chr";
                byte[] fresh = Expose(arc.Read(source), name, cfg);
                if (fresh == null) { log($"{target}: already exposes {name}.cfg — skipped"); continue; }
                arc.Redirect(target, fresh);
                done++;
            }
            log($"borrowed shot effects: {done} redirected");
        }
    }
}
