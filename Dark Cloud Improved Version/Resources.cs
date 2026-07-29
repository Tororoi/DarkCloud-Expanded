using System;
using System.IO;
using System.Reflection;

namespace Dark_Cloud_Improved_Version
{
    internal class Resources
    {
        static string resourcesFolder = "Dark_Cloud_Improved_Version.Resources.";

        // The RubyMemeFix textures are optional: they were never checked into the repo
        // (csproj EmbeddedResource entries are commented out), so builds without them
        // must not crash — missing resources load as empty arrays and CheckElements'
        // WriteByteArray becomes a no-op.
        public static byte[] rubyFireTex = LoadResource("RubyMemeFix.Fire");
        public static byte[] rubyIceTex = LoadResource("RubyMemeFix.Ice");
        public static byte[] rubyThunderTex = LoadResource("RubyMemeFix.Thunder");
        public static byte[] rubyWindTex = LoadResource("RubyMemeFix.Wind");
        public static byte[] rubyHolyTex = LoadResource("RubyMemeFix.Holy");

        static byte[] LoadResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcesFolder + name))
            {
                if (stream == null)
                    return Array.Empty<byte>();

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        static bool warnedMissing = false;

        public static void initiateRubyMemeFix()
        {
            // Textures are loaded eagerly by the static field initializers above;
            // kept so existing call sites (Dungeon, Dayuppy) don't need to change.
            if (rubyFireTex.Length == 0 && !warnedMissing)
            {
                warnedMissing = true;
                Console.WriteLine("RubyMemeFix textures not embedded in this build; ruby texture fix disabled.");
            }
        }
    }
}
