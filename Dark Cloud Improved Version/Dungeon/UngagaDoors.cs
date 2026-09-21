using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Ungaga's doors: the dungeon doors only he opens, made passable for the whole party — the door trigger distances
    /// rewritten as a floor starts (<see cref="Fix"/>) and the swap marker stamped when the party switches to him
    /// (<see cref="CheckSwap"/>).</summary>
    internal static class UngagaDoors
    {
        private static int currentCharCursor = 0;
        private static int prevCharCursor = 0;

        internal static void Fix(byte currentdng)
        {
            // Vanilla-layout addresses inside a dungeon pool: resolve them (DungeonPools — the cat's heap growth moved these
            // pools 880,000 B and this quietly logged "couldn't fix" on every floor), and keep the 150.0 read as the proof
            // that we are looking at the real door distance and not at whatever else now occupies the address.
            long R(long a) => DungeonPools.Resolve(a);
            switch (currentdng)
            {
                case 3:
                    if (Memory.ReadFloat(R(0x20928670)) == 150)
                    {
                        Memory.WriteByte(R(0x20985E0), 30);   // ⚠ 7 hex digits in the original — suspect, unverified
                        Memory.WriteFloat(R(0x20928670), 50);
                        Memory.WriteFloat(R(0x20928928), 50);
                        Memory.WriteByte(R(0x20928B14), 30);
                        Memory.WriteByte(R(0x20928AE4), 30);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Fixed Ungaga Doors");
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Couldn't fix ungaga doors, or they were fixed already");
                    }
                    break;

                case 4:
                    if (Memory.ReadFloat(R(0x2092FA08)) == 150)
                    {
                        Memory.WriteByte(R(0x2092F978), 30);
                        Memory.WriteFloat(R(0x2092FA08), 50);
                        Memory.WriteFloat(R(0x2092FCC0), 50);
                        Memory.WriteByte(R(0x2092FEAC), 30);
                        Memory.WriteByte(R(0x2092FE7C), 30);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Fixed Ungaga Doors");
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Couldn't fix ungaga doors, or they were fixed already");
                    }
                    break;

                case 5:
                    if (Memory.ReadFloat(R(0x209244AC)) == 150)
                    {
                        Memory.WriteByte(R(0x2092441C), 30);
                        Memory.WriteFloat(R(0x209244AC), 50);
                        Memory.WriteFloat(R(0x20924764), 50);
                        Memory.WriteByte(R(0x20924920), 30);
                        Memory.WriteByte(R(0x20924950), 30);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Fixed Ungaga Doors");
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Couldn't fix ungaga doors, or they were fixed already");
                    }
                    break;

                default:
                    break;

            }
        }

        internal static void CheckSwap()
        {
            currentCharCursor = Memory.ReadByte(0x202A2DE8); //current char

            if (currentCharCursor != prevCharCursor)
            {
                if (currentCharCursor == 4)
                {
                    int timer = 0;
                    // Both markers live in a dungeon pool, so they move with the character heap (DungeonPools). The 12850
                    // check gates the WRITE as well as the wait: a write after a timed-out wait would stamp 52 into whatever
                    // holds that address once the pools have moved.
                    long swapA = DungeonPools.Resolve(0x2193A013), swapB = DungeonPools.Resolve(0x217E5453);
                    while (timer < 10)
                    {
                        Thread.Sleep(100);
                        timer++;

                        if (Memory.ReadByte(0x202A2010) == 3)
                        {
                            if (Memory.ReadUShort(swapA) == 12850)
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (Memory.ReadUShort(swapB) == 12850)
                            {
                                break;
                            }
                        }


                    }

                    long swap = Memory.ReadByte(0x202A2010) == 3 ? swapA : swapB;
                    if (Memory.ReadUShort(swap) == 12850)
                    {
                        Memory.WriteByte(swap, 52);
                        Memory.WriteByte(swap + 1, 52);
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"Ungaga swap marker missing at 0x{swap:X} — skipped");
                    }
                }
            }

            prevCharCursor = currentCharCursor;
        }
    }
}
