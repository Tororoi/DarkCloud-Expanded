using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Byte search, log timestamp and pause wait — the helpers with no game subject of their own.
    /// Enemy-slot reads live in <see cref="EnemyQueries"/>, weapon-record reads in <see cref="WeaponRecord"/>.</summary>
    public class ReusableFunctions
    {

        /// <summary>First occurrence of <paramref name="needle"/> in <paramref name="hay"/> at or after
        /// <paramref name="start"/>, else -1. The one byte-search for the ISO/scene patchers.</summary>
        internal static int IndexOfBytes(byte[] hay, byte[] needle, int start = 0)
        {
            for (int i = System.Math.Max(0, start); i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /// <summary>Last occurrence of <paramref name="needle"/> starting at or before
        /// <paramref name="before"/>, else -1.</summary>
        internal static int LastIndexOfBytes(byte[] hay, byte[] needle, int before)
        {
            for (int i = System.Math.Min(before, hay.Length - needle.Length); i >= 0; i--)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns a timestamp to use in the console logs
        /// </summary>
        /// <returns>The timestamp</returns>
        public static string GetDateTimeForLog()
        {
            return "[" + DateTime.Parse(DateTime.UtcNow.ToString()).ToString("HH:mm:ss") + "] ";
        }

        /// <summary>
        /// Puts the current thread to sleep while the game is paused
        /// <br></br>
        /// 0 = Town <br></br>
        /// 1 = Dungeon
        /// </summary>
        /// <param name="mode">0 = Town<br></br>1 = Dungeon</param>
        /// <returns>Returns true when the game is no longer paused</returns>
        public static bool AwaitUnpause(byte mode) {

            while ((mode == 0) ? Player.CheckTownIsPaused() : Player.CheckDunIsPaused())
            {
                Thread.Sleep(100);
                continue;
            }

            return true;
        }
    }
}
