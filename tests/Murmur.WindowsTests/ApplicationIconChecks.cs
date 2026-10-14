using Murmur.App.Windows;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class ApplicationIconChecks
{
    public static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string label) { if (!condition) throw new IOException(label); Console.WriteLine("PASS " + label); passed++; }
        ImageSource Icon(Color color) { var icon = new DrawingImage(new GeometryDrawing(new SolidColorBrush(color), null, new RectangleGeometry(new Rect(0, 0, 16, 16)))); icon.Freeze(); return icon; }
        var codexIcon = Icon(Colors.White); var discordIcon = Icon(Colors.MediumPurple);
        var calls = 0;
        using var cache = new ApplicationIconCache(_ => { Interlocked.Increment(ref calls); return codexIcon; }, 2);
        Check(ApplicationIconCache.DefaultIcon.IsFrozen && ReferenceEquals(cache.Get(null), ApplicationIconCache.DefaultIcon) && ReferenceEquals(cache.Get(@"\\server\app.exe"), ApplicationIconCache.DefaultIcon) && ReferenceEquals(cache.Get("https://example.com/app.exe"), ApplicationIconCache.DefaultIcon) && calls == 0, "Unknown and remote icon paths use a frozen local generic fallback without extraction");
        Check(ActiveApplicationObserver.FriendlyName("ChatGPT", @"C:\Program Files\WindowsApps\OpenAI.Codex_26_x64\app\ChatGPT.exe") == "Codex" && ActiveApplicationObserver.FriendlyName("ChatGPT", @"C:\Apps\ChatGPT.exe") == "ChatGPT", "Installed Codex desktop identity is distinguished from ChatGPT without window titles");
        using var broken = new ApplicationIconCache(_ => throw new System.Runtime.InteropServices.COMException("Malformed icon fixture"), 2);
        _ = broken.Get(@"C:\icon-fixture\broken.exe"); await broken.Pending;
        Check(ReferenceEquals(broken.Get(@"C:\icon-fixture\broken.exe"), ApplicationIconCache.DefaultIcon) && broken.Count == 1, "Malformed native images cache a fallback and release the extraction slot");
        _ = broken.Get(@"C:\icon-fixture\another.exe"); await broken.Pending;
        Check(broken.Count == 2, "Icon extraction remains usable after a native-image failure");
        using var malformedManifest = new ApplicationIconCache(_ => throw new System.Xml.XmlException("Malformed package fixture"), 1);
        _ = malformedManifest.Get(@"C:\icon-fixture\package.exe"); await malformedManifest.Pending;
        Check(ReferenceEquals(malformedManifest.Get(@"C:\icon-fixture\package.exe"), ApplicationIconCache.DefaultIcon), "Malformed package metadata produces a frozen fallback without faulting the extraction task");
        var corruptCalls = 0;
        using var corruptDecoder = new ApplicationIconCache(_ => Interlocked.Increment(ref corruptCalls) == 1 ? throw new FileFormatException("Corrupt PNG decoder fixture") : codexIcon, 2);
        _ = corruptDecoder.Get(@"C:\icon-fixture\corrupt.exe"); await corruptDecoder.Pending;
        var corruptFallback = ReferenceEquals(corruptDecoder.Get(@"C:\icon-fixture\corrupt.exe"), ApplicationIconCache.DefaultIcon);
        _ = corruptDecoder.Get(@"C:\icon-fixture\valid.exe"); await corruptDecoder.Pending;
        Check(corruptFallback && ReferenceEquals(corruptDecoder.Get(@"C:\icon-fixture\valid.exe"), codexIcon) && corruptCalls == 2, "Corrupt image decoder failure uses fallback and leaves the extraction slot usable");
        _ = cache.Get(@"C:\icon-fixture\one.exe"); await cache.Pending;
        Check(ReferenceEquals(cache.Get(@"c:\ICON-FIXTURE\ONE.exe"), codexIcon) && calls == 1, "Icon cache reuses frozen icons case-insensitively");
        _ = cache.Get(@"C:\icon-fixture\two.exe"); await cache.Pending;
        _ = cache.Get(@"C:\icon-fixture\three.exe"); await cache.Pending;
        Check(cache.Count == 2 && calls == 3, "Bounded cache evicts older entries instead of retaining every visited process");
        cache.Dispose(); _ = cache.Get(@"C:\icon-fixture\four.exe");
        Check(cache.Count == 0 && calls == 3, "Disposed icon cache clears managed images and schedules no extraction");
        using var gate = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var concurrentCalls = 0;
        using var busy = new ApplicationIconCache(_ => { Interlocked.Increment(ref concurrentCalls); entered.SetResult(); if (!gate.Wait(3000)) throw new IOException("Fixture deadline"); return discordIcon; }, 2);
        _ = busy.Get(@"C:\icon-fixture\one.exe"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 100; i++) _ = busy.Get(@"C:\icon-fixture\other.exe");
        Check(concurrentCalls == 1 && busy.Count == 0, "Repeated app switches cannot queue an unbounded number of native extractions");
        gate.Set(); await busy.Pending;
        var target = new DraftTarget((nint)1, 10, [1, 2], 0); var context = new RecordingDestinationContext();
        context.Observe(new((nint)1, 10, "Codex", 0, codexIcon)); context.Begin(target, "Codex", codexIcon);
        context.Observe(new((nint)2, 20, "Discord", 1, discordIcon));
        Check(ReferenceEquals(context.Icon, codexIcon) && ReferenceEquals(context.ActiveAppIcon, discordIcon) && context.DestinationChanged, "Focus drift changes the active-app icon but preserves the captured recording icon");
        context.Observe(new((nint)1, 10, "Codex", 2, codexIcon));
        Check(ReferenceEquals(context.Icon, codexIcon) && context.DestinationChanged, "Returning to the original app cannot retarget or clear the retained icon warning");
        context.End(); context.Observe(new((nint)2, 20, "Discord", 3, discordIcon));
        Check(ReferenceEquals(context.Icon, discordIcon), "Idle icon resumes following foreground after the recording context ends");
        using var native = new ApplicationIconCache();
        _ = native.Get(Environment.ProcessPath); await native.Pending.WaitAsync(TimeSpan.FromSeconds(5));
        var nativeIcon = native.Get(Environment.ProcessPath);
        Check(nativeIcon.IsFrozen && (nativeIcon is BitmapSource || ReferenceEquals(nativeIcon, ApplicationIconCache.DefaultIcon)), "Real local executable extraction produces a frozen bitmap or a safe fallback and releases its native handles");
        return passed;
    }
}
