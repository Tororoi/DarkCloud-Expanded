using Avalonia;
using System;
using System.IO;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    static class Program
    {
        public static void Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            Console.WriteLine("Dark Cloud Enhanced - Created by Wordofwind, Dayuppy, MikeZorD, and Plgue");
            Console.WriteLine("Version 1.xxx - Release");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        public static void GetPCSX2Executable()
        {
            int result = Memory.Initialize();

            if (result == -1)
            {
                Console.WriteLine("\nPCSX2 process not found. Is the emulator running?");
                Thread.Sleep(1000);
                return;
            }

            if (!Memory.IsConnected)
            {
                Console.WriteLine("\nPCSX2 found but PINE connection failed. Enable PINE in PCSX2: Settings → Advanced → PINE Server (port 28011)");
                Thread.Sleep(1000);
                return;
            }

            Console.WriteLine("\nConnected to PCSX2 via PINE IPC.");
        }

        public static void ConsoleLogging()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string workPath = Path.GetDirectoryName(exePath);
            string logFolder = Path.Combine(workPath, "EnhancedModLogs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
            var fileStream = new FileStream(
                Path.Combine(logFolder, "EnhancedModLogFile-" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".txt"),
                FileMode.Create);
            var writer = new StreamWriter(fileStream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }

        public static void PressAnyKey()
        {
            Console.WriteLine("\nPress any key to continue . . .");
            Console.ReadKey();
        }
    }
}
