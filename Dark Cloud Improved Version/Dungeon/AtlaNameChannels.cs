using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// What a mod-made atla is called in its pickup ceremony, and which part id carries it. Shared by every feature that places such an
    /// atla (AtlamilliaSword's insurance spheres, AtlaGemron's drops).
    ///  • Sentinel part ids: a mod-made atla's parts entry carries a part id 24-39 (grant-proof, see AtlaSystem), one per pending atla per
    ///    dungeon — each id's EditElementData nameIdx can point at one name at a time. <see cref="TakeSentinel"/> / <see cref="FreeSentinel"/>.
    ///  • Name channels: one of 13 orphaned message pairs in the system bank (meswin/system_1.mes, 633 entries, loaded into the static
    ///    96KB SystemMesBuffer at 0x21CBCA00) — cut-content entries ("Dummy", "Amuleo", "Wise Owl Entry", "Pillar" and blanks) no vanilla
    ///    code path can request (enumerated against the full EditElementData table, including the georama UI readers
    ///    EdEditBuildHelpMes/MoveEditCursor/AtoraNameDraw, which all go through GetAtraMsgNo). The orphan's offset is repointed at the
    ///    atla's name in the bank buffer's free tail and the sentinel's nameIdx aimed at it, so the game resolves the name natively at
    ///    open time; entry 999 (the menus' shared empty-name string) stays vanilla. A channel is released once its ceremony fully ends
    ///    (atraGetStatus back to 0 — releasing earlier could swap the on-screen message back to the orphan's vanilla text).
    /// Header pair offsets are halfword indices biased by +(entryCount + 2). ⚠ GetTextLineDataTop_system (0x14F520) reads the pair offset
    /// as a SIGNED short (short* indexing → lh), so stored offsets must stay ≤ 0x7FFF: channel text must sit below buffer byte ~66800 (at
    /// count=633). The English bank ends at byte 47906.
    /// </summary>
    internal static class AtlaNameChannels
    {
        private const long SysMesBase = 0x21CBCA00;
        private const int ChannelTextBase = 48256;     // free tail, signed-offset safe
        private const int ChannelTextStride = 96;      // 48 words per channel text slot
        private static readonly ushort[] ChannelMsgIds =
            { 1256, 1257, 1258, 1414, 1415, 1614, 1615, 1814, 1885, 1886, 2012, 2069, 2080 };
        private const int ChannelCount = 13;

        private sealed class Cooling
        {
            public int Channel, Dungeon, Sentinel;
            public DateTime Deadline;  // release even if atraGetStatus never settles
        }

        private static bool _bankReady;
        private static readonly long[] _chPairAddr = new long[ChannelCount];      // offset halfword address
        private static readonly ushort[] _chVanillaOff = new ushort[ChannelCount];
        private static readonly ushort[] _chPatchedOff = new ushort[ChannelCount];
        private static readonly string[] _chText = new string[ChannelCount];      // null = free
        private static readonly List<Cooling> _cooling = new List<Cooling>();
        private static readonly HashSet<(int dungeon, int id)> _sentinels = new HashSet<(int, int)>();
        private static DateTime _nextPrepare = DateTime.MinValue;
        private static readonly object _lock = new object();          // AtlamilliaSword and AtlaGemron tick on their own loops

        private static long ChannelTextAddr(int k) => SysMesBase + ChannelTextBase + k * ChannelTextStride;

        /// <summary>A free sentinel part id in <paramref name="dungeon"/>, now taken, or -1 when all 16 are.</summary>
        internal static int TakeSentinel(int dungeon)
        {
            lock (_lock)
            {
                for (int id = AtlaSystem.SentinelPartIdFirst; id <= AtlaSystem.SentinelPartIdLast; id++)
                    if (_sentinels.Add((dungeon, id))) return id;
                return -1;
            }
        }

        internal static void FreeSentinel(int dungeon, int id) { lock (_lock) _sentinels.Remove((dungeon, id)); }

        /// <summary>Scans the bank header (every ~5 s): caches each orphan pair's address, vanilla offset and patched offset, re-applies
        /// text and repoint for claimed channels (a bank reload), and restores entry 999 if an earlier mod version left it repointed.</summary>
        internal static void Prepare()
        {
            lock (_lock)
            {
                if (DateTime.UtcNow < _nextPrepare) return;
                _nextPrepare = DateTime.UtcNow.AddSeconds(5);
                _bankReady = false;
                int cnt = Memory.ReadUShort(SysMesBase);
                if (cnt <= 0 || cnt > 2000) return;                    // bank not loaded / foreign
                byte[] hdr = Memory.ReadBytesBatch(SysMesBase + 4, cnt * 4);
                if (hdr == null) return;

                int textBase = 4 + cnt * 4;
                int found = 0;
                long pair999 = 0, pair803 = 0;
                ushort off999 = 0, off803 = 0;
                for (int i = 0; i < cnt; i++)
                {
                    ushort id = BitConverter.ToUInt16(hdr, i * 4);
                    ushort off = BitConverter.ToUInt16(hdr, i * 4 + 2);
                    if (id == 999) { pair999 = SysMesBase + 4 + i * 4 + 2; off999 = off; }
                    if (id == 803) { pair803 = SysMesBase + 4 + i * 4 + 2; off803 = off; }
                    for (int k = 0; k < ChannelCount; k++)
                    {
                        if (id != ChannelMsgIds[k]) continue;
                        _chPairAddr[k] = SysMesBase + 4 + i * 4 + 2;
                        int wordIdx = (ChannelTextBase + k * ChannelTextStride - textBase) / 2;
                        _chPatchedOff[k] = (ushort)(wordIdx + cnt + 2);
                        // the vanilla offset only while unclaimed (a claimed channel's header word may hold OUR offset)
                        if (_chText[k] == null) _chVanillaOff[k] = off;
                        found++;
                        break;
                    }
                }
                if (found < ChannelCount) return;                      // unexpected bank variant
                _bankReady = true;

                for (int k = 0; k < ChannelCount; k++)                 // claimed channels after a bank reload: text and repoint both hold
                {
                    if (_chText[k] == null) continue;
                    byte[] text = Encode(_chText[k]);
                    byte[] cur = Memory.ReadBytesBatch(ChannelTextAddr(k), text.Length);
                    if (cur == null || !cur.AsSpan().SequenceEqual(text))
                        Memory.WriteByteArray(ChannelTextAddr(k), text);
                    if (Memory.ReadUShort(_chPairAddr[k]) != _chPatchedOff[k])
                        Memory.WriteUShort(_chPairAddr[k], _chPatchedOff[k]);
                }

                // entries 999 and 803 are vanilla twins (both point at the shared ""): if they differ, a previous mod version's 999 repoint
                // is still live — restored
                if (pair999 != 0 && pair803 != 0 && off999 != off803)
                    Memory.WriteUShort(pair999, off803);
            }
        }

        /// <summary>Claims a free channel for the atla carrying <paramref name="sentinel"/>: its name (<paramref name="text"/>, e.g.
        /// "\nGladius SynthSphere") into the channel's tail slot, the orphan pair repointed at it and the sentinel's nameIdx aimed at the
        /// orphan id. The channel index, or -1 (none free / bank not ready: the ceremony shows a blank name).</summary>
        internal static int Claim(int dungeon, int sentinel, string text)
        {
            lock (_lock)
            {
                if (!_bankReady) return -1;
                for (int k = 0; k < ChannelCount; k++)
                {
                    if (_chText[k] != null) continue;
                    _chText[k] = text;
                    Memory.WriteByteArray(ChannelTextAddr(k), Encode(text));
                    Memory.WriteUShort(_chPairAddr[k], _chPatchedOff[k]);
                    Memory.WriteInt(AtlaSystem.NameIdxAddr(dungeon, sentinel), ChannelMsgIds[k] - 1000 - 200 * dungeon);
                    return k;
                }
                return -1;
            }
        }

        /// <summary>The atla was collected (or is gone): its channel and sentinel are released once the ceremony has fully ended.</summary>
        internal static void Cool(int channel, int dungeon, int sentinel)
        {
            lock (_lock) _cooling.Add(new Cooling { Channel = channel, Dungeon = dungeon, Sentinel = sentinel, Deadline = DateTime.UtcNow.AddSeconds(60) });
        }

        /// <summary>Releases at once (the atla never reached a ceremony).</summary>
        internal static void Release(int channel, int dungeon, int sentinel)
        {
            lock (_lock)
            {
                Memory.WriteInt(AtlaSystem.NameIdxAddr(dungeon, sentinel), -1);
                FreeSentinel(dungeon, sentinel);
                if (channel < 0 || channel >= ChannelCount || _chText[channel] == null) return;
                if (_chPairAddr[channel] != 0)
                    Memory.WriteUShort(_chPairAddr[channel], _chVanillaOff[channel]);
                _chText[channel] = null;
            }
        }

        /// <summary>Cooling channels released once their ceremony has fully ended (atraGetStatus back to 0 clears the message in the same
        /// step), or at the deadline as a backstop.</summary>
        internal static void ReleaseCooled()
        {
            lock (_lock)
            {
                if (_cooling.Count == 0) return;
                bool idle = Memory.ReadInt(AtlaSystem.AtraGetStatus) == 0;
                for (int i = _cooling.Count - 1; i >= 0; i--)
                {
                    Cooling c = _cooling[i];
                    if (!idle && DateTime.UtcNow < c.Deadline) continue;
                    Release(c.Channel, c.Dungeon, c.Sentinel);
                    _cooling.RemoveAt(i);
                }
            }
        }

        private static byte[] Encode(string text)
        {
            ushort[] words;
            try { words = WeaponDescriptions.Encode(text); }
            catch (ArgumentException) { words = WeaponDescriptions.Encode("\nSynthSphere"); }
            if ((words.Length + 1) * 2 > ChannelTextStride)        // must fit the channel slot
                words = WeaponDescriptions.Encode("\nSynthSphere");
            byte[] bytes = new byte[(words.Length + 1) * 2];
            for (int i = 0; i < words.Length; i++)
                BitConverter.GetBytes(words[i]).CopyTo(bytes, i * 2);
            BitConverter.GetBytes((ushort)0xFF01).CopyTo(bytes, words.Length * 2);
            return bytes;
        }
    }
}
