using System.Runtime.InteropServices;
using System.Text;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal enum ShimError : int
{
    Success, InvalidArgument, TransportUnavailable, NotTrusted, Restricted,
    InvalidResponse, Limit, Timeout, NoDevice, NoMemory,
}

// Every pointer and delegate is confined to this interop layer. No generic key API.
internal sealed class NativeShimAbi : IDisposable
{
    internal const uint Version = 0x00010000;
    internal static IReadOnlyList<string> ExportNames { get; } = Array.AsReadOnly(new[]
    {
        "cardryft_abi_version", "cardryft_initialize", "cardryft_enumerate_usb",
        "cardryft_open_existing_trust", "cardryft_query_metadata", "cardryft_release",
    });
    [StructLayout(LayoutKind.Sequential)]
    internal struct UsbRecord
    {
        internal uint Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)] internal byte[] Identifier;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ShimError InitializeCall(out IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ShimError EnumerateCall(IntPtr context,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] UsbRecord[] records, uint capacity, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ShimError OpenCall(IntPtr context,
        [In, MarshalAs(UnmanagedType.LPArray)] byte[] identifier, out IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ShimError QueryCall(IntPtr session,
        DeviceMetadataField field, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer, uint capacity, out uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ReleaseCall(IntPtr owner);

    private readonly NativeModuleSet modules;
    private readonly InitializeCall initialize;
    private readonly EnumerateCall enumerate;
    private readonly OpenCall open;
    private readonly QueryCall query;
    private readonly ReleaseCall release;
    private NativeOwnershipHandle? context;

    private NativeShimAbi(NativeModuleSet modules, IntPtr module)
    {
        this.modules = modules;
        T Symbol<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(
            WindowsNativeLoader.GetSymbol(module, name));
        if (Symbol<VersionCall>("cardryft_abi_version")() != Version || Marshal.SizeOf<UsbRecord>() != 136)
            throw new NativeRuntimeRejectedException();
        initialize = Symbol<InitializeCall>("cardryft_initialize");
        enumerate = Symbol<EnumerateCall>("cardryft_enumerate_usb");
        open = Symbol<OpenCall>("cardryft_open_existing_trust");
        query = Symbol<QueryCall>("cardryft_query_metadata");
        release = Symbol<ReleaseCall>("cardryft_release");
    }

    // Offline probe callers use only this factory, InitializeOffline and Dispose.
    internal static NativeShimAbi Load(ValidatedNativeBundle bundle)
    {
        var (modules, root) = WindowsNativeLoader.Load(bundle);
        try { return new NativeShimAbi(modules, root); }
        catch { modules.Dispose(); throw; }
    }

    internal void InitializeOffline()
    {
        if (context is not null) throw new InvalidOperationException("Already initialized.");
        context = modules.WithReference(_ =>
        {
            var error = initialize(out var pointer);
            return OwnResult(error, pointer, modules);
        });
    }

    internal IReadOnlyList<NativeDevice> EnumerateUsb()
    {
        var owner = context ?? throw new InvalidOperationException("Initialize first.");
        return owner.WithReference(pointer =>
        {
            var records = Enumerable.Range(0, 32).Select(_ => new UsbRecord { Identifier = new byte[129] }).ToArray();
            Check(enumerate(pointer, records, 32, out var count));
            return DecodeDevices(records, count);
        });
    }

    internal NativeOwnershipHandle OpenExistingTrust(string identifier)
    {
        if (identifier.Length is 0 or > 128 || identifier.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character != '-')) throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
        var owner = context ?? throw new InvalidOperationException("Initialize first.");
        return owner.WithReference(pointer =>
        {
            var error = open(pointer, Encoding.ASCII.GetBytes(identifier + "\0"), out var session);
            return OwnResult(error, session, owner);
        });
    }

    internal string QueryMetadata(NativeOwnershipHandle session, DeviceMetadataField field)
    {
        if (!Enum.IsDefined(field)) throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
        return session.WithReference(pointer =>
        {
            var buffer = new byte[1025];
            Check(query(pointer, field, buffer, (uint)buffer.Length, out var length));
            return DecodeText(buffer, length);
        });
    }

    private NativeOwnershipHandle OwnResult(ShimError error, IntPtr pointer, SafeHandle parent)
    {
        if (error != ShimError.Success || pointer == IntPtr.Zero)
        {
            if (pointer != IntPtr.Zero) release(pointer);
            if (error == ShimError.Success) throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
            Check(error);
        }
        try { return new NativeOwnershipHandle(pointer, release.Invoke, parent); }
        catch { release(pointer); throw; }
    }

    internal static IReadOnlyList<NativeDevice> DecodeDevices(UsbRecord[] records, uint count)
    {
        if (count > 32 || count > records.Length) throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
        var result = new List<NativeDevice>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var record = records[index];
            if (record.Identifier is null || record.Identifier.Length != 129 || record.Id == 0 || !ids.Add(record.Id))
                throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
            var end = Array.IndexOf(record.Identifier, (byte)0);
            if (end is <= 0 or > 128 || record.Identifier.AsSpan(0, end).ContainsAnyExceptInRange((byte)'-', (byte)'z'))
                throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
            var identifier = Encoding.ASCII.GetString(record.Identifier, 0, end);
            if (identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') || !identifiers.Add(identifier))
                throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
            result.Add(new(identifier, NativeConnectionKind.Usb));
        }
        return result.AsReadOnly();
    }

    internal static string DecodeText(byte[] buffer, uint length)
    {
        if (buffer.Length != 1025 || length > 1024 || buffer[length] != 0 || buffer.AsSpan(0, (int)length).Contains((byte)0))
            throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
        try
        {
            var text = new UTF8Encoding(false, true).GetString(buffer, 0, (int)length);
            if (text.Length > 256) throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
            return text;
        }
        catch (DecoderFallbackException) { throw new NativeDeviceException(NativeDeviceError.InvalidResponse); }
    }

    internal static void Check(ShimError error)
    {
        if (error == ShimError.Success) return;
        throw new NativeDeviceException(error switch
        {
            ShimError.TransportUnavailable => NativeDeviceError.TransportUnavailable,
            ShimError.NotTrusted => NativeDeviceError.NotTrusted,
            ShimError.Restricted => NativeDeviceError.Restricted,
            ShimError.NoDevice => NativeDeviceError.Disconnected,
            ShimError.Timeout => NativeDeviceError.Timeout,
            ShimError.NoMemory => NativeDeviceError.Unknown,
            _ => NativeDeviceError.InvalidResponse,
        });
    }

    public void Dispose() { context?.Dispose(); modules.Dispose(); }
}

internal sealed class NativeOwnershipHandle : SafeHandle
{
    private readonly object gate = new();
    private readonly Action<IntPtr> release;
    private readonly SafeHandle? parent;
    private readonly bool parentHeld;
    internal NativeOwnershipHandle(IntPtr pointer, Action<IntPtr> release, SafeHandle? parent = null) : base(IntPtr.Zero, true)
    {
        if (pointer == IntPtr.Zero) throw new ArgumentException("Owner cannot be null.");
        this.release = release;
        this.parent = parent;
        if (parent is not null) { var held = false; parent.DangerousAddRef(ref held); parentHeld = held; }
        SetHandle(pointer);
    }
    public override bool IsInvalid => handle == IntPtr.Zero;
    internal T WithReference<T>(Func<IntPtr, T> operation)
    {
        lock (gate)
        {
            var held = false;
            try { DangerousAddRef(ref held); return operation(handle); }
            finally { if (held) DangerousRelease(); }
        }
    }
    protected override bool ReleaseHandle()
    {
        try { release(handle); return true; }
        finally { if (parentHeld) parent!.DangerousRelease(); }
    }
}
