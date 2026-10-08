using System.Runtime.InteropServices;
using System.Text;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal static class WindowsNativeLoader
{
    private const uint SafeSearchFlags = 0x00000100 | 0x00000800; // DLL_LOAD_DIR | SYSTEM32
    private static readonly string[] LoadOrder = ["libcrypto-3-x64.dll", "libssl-3-x64.dll", "libplist-2.0.dll", "cardryft-device.dll"];
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder path, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FreeLibrary(IntPtr module);

    internal static (NativeModuleSet Modules, IntPtr Root) Load(ValidatedNativeBundle bundle)
    {
        if (!OperatingSystem.IsWindows() || !bundle.Files.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(
            LoadOrder.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) throw new NativeRuntimeRejectedException();
        var loaded = new List<IntPtr>();
        try
        {
            // Reject preloaded native basenames, including same-path images whose on-disk bytes could have changed.
            if (LoadOrder.Any(name => GetModuleHandleW(name) != IntPtr.Zero)) throw new NativeRuntimeRejectedException();
            foreach (var name in LoadOrder)
            {
                var path = Path.Combine(bundle.Directory, name);
                var module = LoadLibraryExW(path, IntPtr.Zero, SafeSearchFlags);
                if (module == IntPtr.Zero) throw new NativeRuntimeRejectedException();
                loaded.Add(module);
                var actual = new StringBuilder(32768);
                var length = GetModuleFileNameW(module, actual, (uint)actual.Capacity);
                if (length == 0 || length >= actual.Capacity || !string.Equals(actual.ToString(), path, StringComparison.OrdinalIgnoreCase))
                    throw new NativeRuntimeRejectedException();
            }
            return (new NativeModuleSet(loaded), loaded[^1]);
        }
        catch { foreach (var module in loaded.AsEnumerable().Reverse()) FreeLibrary(module); throw new NativeRuntimeRejectedException(); }
    }

    internal static IntPtr GetSymbol(IntPtr module, string name)
    {
        var symbol = GetProcAddress(module, name);
        return symbol != IntPtr.Zero ? symbol : throw new NativeRuntimeRejectedException();
    }
}

internal sealed class NativeModuleSet(IReadOnlyList<IntPtr> modules) : SafeHandle(new IntPtr(1), true)
{
    public override bool IsInvalid => handle == IntPtr.Zero;
    internal T WithReference<T>(Func<IntPtr, T> operation)
    {
        var held = false;
        try { DangerousAddRef(ref held); return operation(handle); }
        finally { if (held) DangerousRelease(); }
    }
    protected override bool ReleaseHandle()
    {
        foreach (var module in modules.Reverse()) WindowsNativeLoader.FreeLibrary(module);
        return true;
    }
}
