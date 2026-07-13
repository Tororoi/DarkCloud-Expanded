using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    class Memory
    {
        internal static string procName = "pcsx2";
        internal static Process process;

        private static Socket _socket;
        private static NetworkStream _stream;
        private static readonly object _lock = new object();


        public static void Connect(int slot = 0)
        {
            _stream?.Close();
            _socket?.Close();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.NoDelay = true;
                _socket.Connect(new IPEndPoint(IPAddress.Loopback, 28011 + slot));
            }
            else
            {
                string sockPath = Path.Combine(Path.GetTempPath(),
                    slot == 0 ? "pcsx2.sock" : $"pcsx2_slot{slot}.sock");
                _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                _socket.Connect(new UnixDomainSocketEndPoint(sockPath));
            }

            _stream = new NetworkStream(_socket, ownsSocket: false);
            _writeFailCount = 0;
            _writeProbeDone = false;
        }

        public static bool IsConnected => _socket?.Connected ?? false;

        public static int Initialize()
        {
            process = GetProcess(procName);
            if (process == null)
                return -1;
            try
            {
                Connect();
                QueryVersion();
                ProbeWriteOpcodes();
                ModWindow.NightlyVersionCheck();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("PINE connection failed: " + ex.Message);
                Console.WriteLine("Ensure PINE is enabled in PCSX2: Settings → Advanced → PINE Server (port 28011)");
                return -2;
            }
        }

        public static Process GetProcess(string name)
        {
            name = name.ToLowerInvariant().Trim();
            Process found = null;
            int count = 0;
            foreach (Process p in Process.GetProcesses())
            {
                if (p.ProcessName.ToLowerInvariant().Trim().Contains(name))
                {
                    found = p;
                    count++;
                }
            }
            if (count > 1)
                Console.WriteLine("Found {0} running instances of {1}. Using last found.", count, found?.ProcessName);
            return found;
        }

        private static int _writeFailCount = 0;
        private static bool _writeProbeDone = false;
        private static bool _altWriteOpcodes = false; // true = use 0x04/0x05/0x06 (current PCSX2), false = 0x08/0x09/0x0A (legacy)
        private static byte OpWrite8  => _altWriteOpcodes ? (byte)0x04 : (byte)0x08;
        private static byte OpWrite16 => _altWriteOpcodes ? (byte)0x05 : (byte)0x09;
        private static byte OpWrite32 => _altWriteOpcodes ? (byte)0x06 : (byte)0x0A;
        private static byte OpWrite64 => _altWriteOpcodes ? (byte)0x07 : (byte)0x0B;
        private static void DisconnectStream()
        {
            _stream?.Close();
            _socket?.Close();
            _stream = null;
            _socket = null;
        }

        internal static void WriteIntFast(long address, int value)
        {
            SendBatch(BuildWritePacket(OpWrite32, address, BitConverter.GetBytes(value)));
        }

        private static byte[] SendBatch(byte[] packet)
        {
            lock (_lock)
            {
                if (_stream == null) return Array.Empty<byte>();
                try
                {
                    _stream.Write(packet, 0, packet.Length);
                    var lenBuf = new byte[4];
                    ReadFully(lenBuf, 0, 4);
                    int respLen = BitConverter.ToInt32(lenBuf, 0) - 4;
                    if (respLen <= 0) return Array.Empty<byte>();
                    var resp = new byte[respLen];
                    ReadFully(resp, 0, respLen);
                    // resp[0] is the per-command error code: 0x00 = IPC_OK, 0xFF = IPC_FAIL
                    if (resp[0] != 0)
                    {
                        byte opcode = packet.Length > 4 ? packet[4] : (byte)0;
                        bool isWrite = (opcode >= 0x04 && opcode <= 0x07) || (opcode >= 0x08 && opcode <= 0x0B);
                        if (isWrite)
                        {
                            _writeFailCount++;
                            if (_writeFailCount <= 3)
                                Console.WriteLine($"PINE write fail #{_writeFailCount} (opcode 0x{opcode:X2})");
                            if (_writeFailCount == 5)
                            {
                                Console.WriteLine("Multiple PINE write failures. PCSX2 may be paused or this build doesn't support writes.");
                                ModWindow.PineWritesFailing();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"PINE IPC_FAIL on read (opcode 0x{opcode:X2})");
                        }
                        return Array.Empty<byte>();
                    }
                    if (respLen < 2) return Array.Empty<byte>();
                    var payload = new byte[respLen - 1];
                    Array.Copy(resp, 1, payload, 0, respLen - 1);
                    return payload;
                }
                catch (EndOfStreamException)
                {
                    DisconnectStream();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[PINE] Stream closed — emulator disconnected.");
                    return Array.Empty<byte>();
                }
                catch (IOException ex)
                {
                    DisconnectStream();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[PINE] IO error — connection lost: {ex.Message}");
                    return Array.Empty<byte>();
                }
            }
        }

        private static void ReadFully(byte[] buf, int offset, int count)
        {
            while (count > 0)
            {
                int read = _stream.Read(buf, offset, count);
                if (read == 0) throw new System.IO.EndOfStreamException("PINE stream closed.");
                offset += read;
                count -= read;
            }
        }

        private static void QueryVersion()
        {
            // 0x08 = MsgVersion (PINE/PCSX2 version), 0x0E = MsgGameVersion (game version)
            (byte op, string label)[] queries = { (0x08, "PINE version"), (0x0E, "Game version") };
            foreach (var (op, label) in queries)
            {
                var pkt = new byte[5];
                BitConverter.GetBytes(5).CopyTo(pkt, 0);
                pkt[4] = op;
                try
                {
                    lock (_lock)
                    {
                        _stream.Write(pkt, 0, pkt.Length);
                        var lenBuf = new byte[4];
                        ReadFully(lenBuf, 0, 4);
                        int n = BitConverter.ToInt32(lenBuf, 0) - 4;
                        if (n > 0)
                        {
                            var r = new byte[n];
                            ReadFully(r, 0, n);
                            if (r[0] == 0 && n > 1)
                            {
                                string raw = Encoding.UTF8.GetString(r, 1, n - 1);
                                string ver = new string(raw.Where(c => c >= ' ').ToArray()).Trim();
                                if (!string.IsNullOrEmpty(ver))
                                    Console.WriteLine($"{label}: {ver}");
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void ProbeWriteOpcodes()
        {
            if (_writeProbeDone) return;
            _writeProbeDone = true;

            // Try current PCSX2 PINE spec: Write8 = 0x04
            SendBatch(BuildWritePacket(0x04, 0x21F10024, new byte[] { 0x01 }));
            if (ReadByte(0x21F10024) == 0x01)
            {
                _altWriteOpcodes = true;
                Console.WriteLine("[PINE probe] Write8 opcode 0x04 works (current PCSX2 PINE spec).");
                goto Done;
            }

            // Fallback: legacy PCSX2 PINE spec: Write8 = 0x08
            SendBatch(BuildWritePacket(0x08, 0x21F10024, new byte[] { 0x01 }));
            if (ReadByte(0x21F10024) == 0x01)
            {
                Console.WriteLine("[PINE probe] Write8 opcode 0x08 works (legacy PCSX2 PINE spec).");
                goto Done;
            }

            Console.WriteLine("[PINE probe] All write strategies failed. Memory writes will not work.");

            Done:
            _writeFailCount = 0;
            WriteByte(0x21F10024, 0x00); // Clear probe value so instance-check in MainMenuThread sees 0
            _writeFailCount = 0;
        }

        // PCSX2 address-space constants.
        // The PS2 EE is MIPS: segment bits occupy the upper 3 bits of a 32-bit virtual address
        // (kseg0/kseg1/kuseg all alias the same physical RAM). Strip them with PhysAddrMask to get the
        // physical byte offset, then add Pcsx2Base to land in PCSX2's RAM window (where PINE reads/writes).
        internal const uint PhysAddrMask = 0x1FFFFFFF; // strips MIPS segment bits → 29-bit physical address
        internal const long Pcsx2Base    = 0x20000000; // PCSX2 maps PS2 physical RAM at this offset
        internal const uint EeRamSize    = 0x02000000; // 32 MB — the EE's entire main RAM (guest 0x0..0x01FFFFFF)

        // Convert a PS2-native pointer (read from PS2 RAM) to a PCSX2-addressable address.
        internal static long ToMmu(long nativePtr) => (nativePtr & PhysAddrMask) | Pcsx2Base;

        /// <summary>Is <paramref name="guestPtr"/> a usable PS2 pointer — non-null and inside the EE's 32 MB of
        /// RAM? Pointers chased out of live game memory (model trees, map/fire structs, cloth lists) are null or
        /// stale garbage during loads and transitions, so guard before dereferencing: without this we'd compute
        /// an address from nonsense and scribble into unrelated memory. Expects a GUEST address — mask an MMU
        /// address with <see cref="PhysAddrMask"/> first (that's the usual `ReadInt(...) &amp; PhysAddrMask` idiom).</summary>
        internal static bool IsValidGuest(uint guestPtr) => guestPtr != 0 && guestPtr < EeRamSize;

        /// <inheritdoc cref="IsValidGuest(uint)"/>
        internal static bool IsValidGuest(long guestPtr) => guestPtr > 0 && guestPtr < EeRamSize;

        private static uint PhysAddr(long address) => (uint)(address & PhysAddrMask);

        private static byte[] BuildReadPacket(byte opcode, long address)
        {
            var pkt = new byte[9];
            BitConverter.GetBytes(9).CopyTo(pkt, 0);
            pkt[4] = opcode;
            BitConverter.GetBytes(PhysAddr(address)).CopyTo(pkt, 5);
            return pkt;
        }

        private static byte[] BuildWritePacket(byte opcode, long address, byte[] value)
        {
            int totalLen = 4 + 1 + 4 + value.Length;
            var pkt = new byte[totalLen];
            BitConverter.GetBytes(totalLen).CopyTo(pkt, 0);
            pkt[4] = opcode;
            BitConverter.GetBytes(PhysAddr(address)).CopyTo(pkt, 5);
            value.CopyTo(pkt, 9);
            return pkt;
        }

        internal static byte ReadByte(long address)
        {
            var r = SendBatch(BuildReadPacket(0x00, address));
            return r.Length >= 1 ? r[0] : (byte)0;
        }

        internal static byte[] ReadByteArray(long address, long numBytes)
        {
            var result = new byte[numBytes];
            for (long i = 0; i < numBytes; i++)
                result[i] = ReadByte(address + i);
            return result;
        }

        /// <summary>
        /// Bulk byte read via packed Read32 batches (one PINE round-trip per 2048 words instead
        /// of one per BYTE — use this instead of <see cref="ReadByteArray"/> for anything larger
        /// than a few bytes). <paramref name="address"/> may be unaligned; alignment is handled
        /// internally.
        /// </summary>
        internal static byte[] ReadBytesBatch(long address, int numBytes)
        {
            const int ChunkWords = 2048;   // 8KB of data per round-trip
            long alignedStart = address & ~3L;
            int alignedLen = (int)(((address + numBytes + 3) & ~3L) - alignedStart);
            int totalWords = alignedLen / 4;
            var buf = new byte[alignedLen];
            for (int done = 0; done < totalWords; done += ChunkWords)
            {
                int words = Math.Min(ChunkWords, totalWords - done);
                uint[] chunk = ReadUIntBatch(alignedStart + (long)done * 4, words);
                if (chunk == null || chunk.Length != words) return null;
                for (int i = 0; i < words; i++)
                    BitConverter.GetBytes(chunk[i]).CopyTo(buf, (done + i) * 4);
            }
            var result = new byte[numBytes];
            Array.Copy(buf, (int)(address - alignedStart), result, 0, numBytes);
            return result;
        }

        // Reads <count> consecutive 32-bit floats starting at <startAddr> in one PINE
        // round-trip. Packs N Read32 commands into a single socket packet.
        //
        // PCSX2 v2.7.x batch response format (confirmed empirically):
        //   [uint32 total_len][0x00 status][float_0][float_1]...[float_N-1]
        //   i.e. one shared status byte followed by N×4 data bytes — NOT N×(status+data).
        // The parser handles both formats so it degrades gracefully if PCSX2 changes.
        internal static float[] ReadFloatBatch(long startAddr, int count)
        {
            int pktLen = 4 + count * 5; // header(4) + N*(opcode(1)+addr(4))
            var pkt = new byte[pktLen];
            BitConverter.GetBytes(pktLen).CopyTo(pkt, 0);
            for (int i = 0; i < count; i++)
            {
                int off = 4 + i * 5;
                pkt[off] = 0x02; // Read32
                BitConverter.GetBytes(PhysAddr(startAddr + (long)i * 4)).CopyTo(pkt, off + 1);
            }

            lock (_lock)
            {
                if (_stream == null) return new float[count];
                try
                {
                    _stream.Write(pkt, 0, pkt.Length);
                    var lenBuf = new byte[4];
                    ReadFully(lenBuf, 0, 4);
                    int respLen = BitConverter.ToInt32(lenBuf, 0) - 4;
                    if (respLen <= 0) return new float[count];
                    var resp = new byte[respLen];
                    ReadFully(resp, 0, respLen);

                    var results = new float[count];
                    if (respLen >= count * 5)
                    {
                        // fmtA: N × (status_byte + 4_data_bytes)
                        for (int i = 0; i < count; i++)
                        {
                            int off = i * 5;
                            if (resp[off] == 0)
                                results[i] = BitConverter.ToSingle(resp, off + 1);
                        }
                    }
                    else if (respLen >= 1 + count * 4 && resp[0] == 0)
                    {
                        // fmtB (PCSX2 v2.7.x): single status byte + N × 4_data_bytes
                        for (int i = 0; i < count; i++)
                            results[i] = BitConverter.ToSingle(resp, 1 + i * 4);
                    }
                    else if (respLen >= 5 && resp[0] == 0)
                    {
                        results[0] = BitConverter.ToSingle(resp, 1);
                    }
                    return results;
                }
                catch (EndOfStreamException)
                {
                    DisconnectStream();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[PINE] Stream closed — emulator disconnected.");
                    return new float[count];
                }
                catch (IOException ex)
                {
                    DisconnectStream();
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[PINE] IO error — connection lost: {ex.Message}");
                    return new float[count];
                }
            }
        }

        // Reads <count> consecutive 32-bit words starting at <startAddr> in ONE PINE round-trip
        // (batched Read32). Mirrors ReadFloatBatch. Use for fast block scans — ReadByteArray is
        // one round-trip PER BYTE and is far too slow for large ranges.
        internal static uint[] ReadUIntBatch(long startAddr, int count)
        {
            int pktLen = 4 + count * 5;
            var pkt = new byte[pktLen];
            BitConverter.GetBytes(pktLen).CopyTo(pkt, 0);
            for (int i = 0; i < count; i++)
            {
                int off = 4 + i * 5;
                pkt[off] = 0x02; // Read32
                BitConverter.GetBytes(PhysAddr(startAddr + (long)i * 4)).CopyTo(pkt, off + 1);
            }
            lock (_lock)
            {
                if (_stream == null) return new uint[count];
                try
                {
                    _stream.Write(pkt, 0, pkt.Length);
                    var lenBuf = new byte[4];
                    ReadFully(lenBuf, 0, 4);
                    int respLen = BitConverter.ToInt32(lenBuf, 0) - 4;
                    if (respLen <= 0) return new uint[count];
                    var resp = new byte[respLen];
                    ReadFully(resp, 0, respLen);
                    var results = new uint[count];
                    if (respLen >= count * 5)                       // fmtA: N × (status + 4 data)
                    {
                        for (int i = 0; i < count; i++)
                            if (resp[i * 5] == 0) results[i] = BitConverter.ToUInt32(resp, i * 5 + 1);
                    }
                    else if (respLen >= 1 + count * 4 && resp[0] == 0) // fmtB: status + N × 4 data
                    {
                        for (int i = 0; i < count; i++)
                            results[i] = BitConverter.ToUInt32(resp, 1 + i * 4);
                    }
                    return results;
                }
                catch (EndOfStreamException) { DisconnectStream(); return new uint[count]; }
                catch (IOException) { DisconnectStream(); return new uint[count]; }
            }
        }

        internal static ushort ReadUShort(long address)
        {
            var r = SendBatch(BuildReadPacket(0x01, address));
            return r.Length >= 2 ? BitConverter.ToUInt16(r, 0) : (ushort)0;
        }

        internal static short ReadShort(long address)
        {
            var r = SendBatch(BuildReadPacket(0x01, address));
            return r.Length >= 2 ? BitConverter.ToInt16(r, 0) : (short)0;
        }

        internal static uint ReadUInt(long address)
        {
            var r = SendBatch(BuildReadPacket(0x02, address));
            return r.Length >= 4 ? BitConverter.ToUInt32(r, 0) : 0u;
        }

        internal static int ReadInt(long address)
        {
            var r = SendBatch(BuildReadPacket(0x02, address));
            return r.Length >= 4 ? BitConverter.ToInt32(r, 0) : 0;
        }

        internal static float ReadFloat(long address)
        {
            // New PINE spec: 0x04 = Write8, not ReadFloat. Use Read32 (0x02) and reinterpret bytes.
            var r = SendBatch(BuildReadPacket(0x02, address));
            return r.Length >= 4 ? BitConverter.ToSingle(r, 0) : 0f;
        }

        internal static double ReadDouble(long address)
        {
            var r = SendBatch(BuildReadPacket(0x03, address));
            return r.Length >= 8 ? BitConverter.ToDouble(r, 0) : 0.0;
        }

        internal static long ReadLong(long address)
        {
            var r = SendBatch(BuildReadPacket(0x03, address));
            return r.Length >= 8 ? BitConverter.ToInt64(r, 0) : 0L;
        }

        internal static string ReadString(long address, long length)
        {
            return Encoding.GetEncoding(10000).GetString(ReadByteArray(address, length));
        }

        internal static bool WriteByte(long address, byte value)
        {
            SendBatch(BuildWritePacket(OpWrite8, address, new[] { value }));
            return true;
        }

        internal static bool WriteOneByte(long address, byte[] value)
        {
            return WriteByte(address, value[0]);
        }

        internal static void WriteByteArray(long address, byte[] byteArray)
        {
            Write(address, byteArray);
        }

        // Writes <data.Length> consecutive bytes starting at <startAddr>, packing many Write8 commands into
        // each PINE packet (chunked) — the byte-wise sibling of WriteUIntBatch, for bulk blobs (e.g. texture
        // pixels) where per-byte round-trips would take seconds and alignment rules out 32-bit packing.
        internal static void WriteBytesBatch(long startAddr, byte[] data)
        {
            const int chunk = 500;                 // 500*6+4 = 3004 bytes/packet — well under the PINE buffer
            byte op = OpWrite8;
            for (int start = 0; start < data.Length; start += chunk)
            {
                int n = Math.Min(chunk, data.Length - start);
                int pktLen = 4 + n * 6;            // header + n*(opcode(1)+addr(4)+value(1))
                var pkt = new byte[pktLen];
                BitConverter.GetBytes(pktLen).CopyTo(pkt, 0);
                for (int i = 0; i < n; i++)
                {
                    int off = 4 + i * 6;
                    pkt[off] = op;
                    BitConverter.GetBytes(PhysAddr(startAddr + start + i)).CopyTo(pkt, off + 1);
                    pkt[off + 5] = data[start + i];
                }
                SendBatch(pkt);
            }
        }

        internal static bool WriteUShort(long address, ushort value)
        {
            SendBatch(BuildWritePacket(OpWrite16, address, BitConverter.GetBytes(value)));
            return true;
        }

        internal static bool WriteInt(long address, int value)
        {
            SendBatch(BuildWritePacket(OpWrite32, address, BitConverter.GetBytes(value)));
            return true;
        }

        // Writes <values.Length> consecutive 32-bit words starting at <startAddr>, packing many Write32 commands
        // into each PINE packet (chunked) so a large block moves in a handful of round-trips instead of one per
        // word. Used for slot-block swaps where per-word writes would be far too slow and leave the running game
        // reading half-written state.
        internal static void WriteUIntBatch(long startAddr, uint[] values)
        {
            const int chunk = 400;                 // 400*9 = 3604 bytes/packet — well under the PINE buffer
            byte op = OpWrite32;
            for (int start = 0; start < values.Length; start += chunk)
            {
                int n = Math.Min(chunk, values.Length - start);
                int pktLen = 4 + n * 9;            // header + n*(opcode(1)+addr(4)+value(4))
                var pkt = new byte[pktLen];
                BitConverter.GetBytes(pktLen).CopyTo(pkt, 0);
                for (int i = 0; i < n; i++)
                {
                    int off = 4 + i * 9;
                    pkt[off] = op;
                    BitConverter.GetBytes(PhysAddr(startAddr + (long)(start + i) * 4)).CopyTo(pkt, off + 1);
                    BitConverter.GetBytes(values[start + i]).CopyTo(pkt, off + 5);
                }
                SendBatch(pkt);
            }
        }

        internal static bool WriteUInt(long address, uint value)
        {
            SendBatch(BuildWritePacket(OpWrite32, address, BitConverter.GetBytes(value)));
            return true;
        }

        internal static bool WriteFloat(long address, float value)
        {
            // No dedicated WriteFloat opcode in current PINE spec — write raw bytes as Write32
            SendBatch(BuildWritePacket(OpWrite32, address, BitConverter.GetBytes(value)));
            return true;
        }

        internal static bool WriteDouble(long address, double value)
        {
            SendBatch(BuildWritePacket(OpWrite64, address, BitConverter.GetBytes(value)));
            return true;
        }

        internal static bool Write(long address, byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
                WriteByte(address + i, value[i]);
            return true;
        }

        internal static bool WriteString(long address, string stringToWrite)
        {
            return Write(address, Encoding.GetEncoding(10000).GetBytes(stringToWrite));
        }

        internal static List<long> StringSearch(long startOffset, long stopOffset, string searchString)
        {
            var results = new List<long>();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Searching for " + searchString + ". This may take awhile.");
            for (long cur = startOffset; cur < stopOffset; cur++)
            {
                if (ReadString(cur, searchString.Length) == searchString)
                    results.Add(cur);
            }
            return results;
        }

        internal static List<long> IntSearch(long startOffset, long stopOffset, int searchValue)
        {
            var results = new List<long>();
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Searching for " + searchValue + ". This may take awhile.");
            for (long cur = startOffset; cur < stopOffset; cur++)
            {
                if (ReadInt(cur) == searchValue)
                    results.Add(cur);
            }
            return results;
        }

        internal static List<long> ByteArraySearch(long startOffset, long stopOffset, byte[] byteArray)
        {
            var results = new List<long>();
            for (long cur = startOffset; cur < stopOffset; cur++)
            {
                bool match = true;
                for (int i = 0; i < byteArray.Length && match; i++)
                    match = ReadByte(cur + i) == byteArray[i];
                if (match) results.Add(cur);
            }
            return results;
        }
    }
}
