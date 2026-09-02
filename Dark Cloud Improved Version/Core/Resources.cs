using System;
using System.IO;
using System.Reflection;

namespace Dark_Cloud_Improved_Version
{
    internal class Resources
    {
        static string resourcesFolder = "Dark_Cloud_Improved_Version.Resources.";

        static Stream rubyFire = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + "RubyMemeFix.Fire");
        static Stream rubyIce = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + "RubyMemeFix.Ice");
        static Stream rubyThunder = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + "RubyMemeFix.Thunder");
        static Stream rubyWind = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + "RubyMemeFix.Wind");
        static Stream rubyHoly = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + "RubyMemeFix.Holy");

        // Null when the untracked RubyMemeFix resources aren't bundled (the .csproj includes are Exists()-gated).
        // Guard so a clean build still RUNS: empty buffers + a no-op loader leave the element-texture feature
        // disabled instead of crashing static init (and the writer's WriteByteArray of an empty buffer is a no-op).
        public static byte[] rubyFireTex = new byte[rubyFire?.Length ?? 0];
        public static byte[] rubyIceTex = new byte[rubyIce?.Length ?? 0];
        public static byte[] rubyThunderTex = new byte[rubyThunder?.Length ?? 0];
        public static byte[] rubyWindTex = new byte[rubyWind?.Length ?? 0];
        public static byte[] rubyHolyTex = new byte[rubyHoly?.Length ?? 0];

        public static bool RubyMemeFixAvailable =>
            rubyFire != null && rubyIce != null && rubyThunder != null && rubyWind != null && rubyHoly != null;

        public static void initiateRubyMemeFix()
        {
            if (!RubyMemeFixAvailable) return;   // resources not bundled (clean build) — feature stays off

            for (int i = 0; i < rubyFire.Length; i++)
                rubyFireTex[i] = (byte)rubyFire.ReadByte();

            for (int i = 0; i < rubyIce.Length; i++)
                rubyIceTex[i] = (byte)rubyIce.ReadByte();

            for (int i = 0; i < rubyThunder.Length; i++)
                rubyThunderTex[i] = (byte)rubyThunder.ReadByte();

            for (int i = 0; i < rubyWind.Length; i++)
                rubyWindTex[i] = (byte)rubyWind.ReadByte();

            for (int i = 0; i < rubyHoly.Length; i++)
                rubyHolyTex[i] = (byte)rubyHoly.ReadByte();
        }
    }
}