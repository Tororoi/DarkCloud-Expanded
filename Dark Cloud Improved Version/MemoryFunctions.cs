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
        private static byte[] SendBatch(byte[] packet)
        {
            lock (_lock)
            {
                if (_stream == null) throw new InvalidOperationException("PINE not connected.");
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

        private static uint PhysAddr(long address) => (uint)(address & 0x1FFFFFFF);

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
