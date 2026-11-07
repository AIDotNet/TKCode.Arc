using System.Runtime.InteropServices;

namespace TKCode.Arc.Video.Native.Tests;

/// <summary>
/// Entry point: runs the xunit runner on a worker thread while the main thread services the main run loop on macOS.
/// AVPlayer only becomes ready and advances time when the main dispatch queue runs (verified: without it the item never
/// leaves AVPlayerItemStatusUnknown), and an Avalonia app's main thread always runs it.
/// </summary>
internal static partial class Program
{
    private static volatile bool s_done;

    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return Run(args);
        }
        MainRunLoop.IsServiced = true;
        var exitCode = 0;
        var worker = new Thread(() =>
        {
            try
            {
                exitCode = Run(args);
            }
            finally
            {
                s_done = true;
            }
        })
        {
            Name = "xunit",
            IsBackground = false,
        };
        worker.Start();
        while (!s_done)
        {
            MainRunLoop.RunOnce(TimeSpan.FromMilliseconds(50));
        }
        worker.Join();
        return exitCode;
    }

    private static int Run(string[] args)
    {
        if (args.Any(arg => arg is "--server" or "--internal-msbuild-node"))
        {
            return Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(args, SelfRegisteredExtensions.AddSelfRegisteredExtensions).GetAwaiter().GetResult();
        }
        return Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args).GetAwaiter().GetResult();
    }
}

/// <summary>The macOS main run loop, through CoreFoundation.</summary>
internal static partial class MainRunLoop
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private static nint s_defaultMode;

    /// <summary>True when <see cref="Program.Main"/> services the main run loop (false under a host that loads the tests itself).</summary>
    public static bool IsServiced { get; set; }

    public static void RunOnce(TimeSpan timeout)
    {
        if (s_defaultMode == 0)
        {
            var library = NativeLibrary.Load(CoreFoundation);
            s_defaultMode = Marshal.ReadIntPtr(NativeLibrary.GetExport(library, "kCFRunLoopDefaultMode"));
        }
        var result = CFRunLoopRunInMode(s_defaultMode, timeout.TotalSeconds, 0);
        if (result == 1)
        {
            // kCFRunLoopRunFinished: no sources yet; the main queue is drained on the next pass.
            Thread.Sleep(timeout);
        }
    }

    [LibraryImport(CoreFoundation)]
    private static partial int CFRunLoopRunInMode(nint mode, double seconds, byte returnAfterSourceHandled);
}
