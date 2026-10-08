using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardryft.Device.Apple.LibimobileDevice;

namespace Cardryft.Tests;

public sealed class HardenedNativeRuntimeTests
{
    [Fact]
    public void CompiledPins_MatchNewAuditedRuntime_WithoutApprovingPromotion()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(System.IO.Path.Combine(root.FullName, "Cardryft.sln")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root missing.");
        using var evidence = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(root.FullName,
            "native-build", "evidence", "milestone4f-results.json")));
        var files = evidence.RootElement.GetProperty("runtimeFiles").EnumerateArray().ToArray();
        Assert.Equal(4, HardenedRuntimeManifest.Pins.Count);
        Assert.Equal(4, files.Length);
        Assert.False(HardenedRuntimeManifest.PromotionApproved);
        foreach (var pin in HardenedRuntimeManifest.Pins)
        {
            var file = Assert.Single(files, file => file.GetProperty("File").GetString() == pin.Name);
            Assert.Equal(file.GetProperty("SHA256").GetString(), pin.Sha256);
            Assert.Equal(file.GetProperty("Size").GetInt64(), pin.Size);
            Assert.Equal("0x8664", file.GetProperty("Machine").GetString());
            Assert.Equal(file.GetProperty("Imports").EnumerateArray().Select(item => item.GetString()), pin.Imports);
        }
    }

    [Fact]
    public async Task ConcurrentDisposal_CannotReleaseActiveWork()
    {
        var releases = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finish = new ManualResetEventSlim();
        var owner = new NativeOwnershipHandle(new IntPtr(1), _ => Interlocked.Increment(ref releases));
        var token = TestContext.Current.CancellationToken;
        var first = Task.Run(() => owner.WithReference(_ =>
        { entered.SetResult(); Assert.True(finish.Wait(TimeSpan.FromSeconds(5), token)); return 0; }), token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            owner.Dispose();
            Assert.Equal(0, releases);
        }
        finally { finish.Set(); owner.Dispose(); }
        await first.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(1, releases);
        Assert.Throws<ObjectDisposedException>(() => owner.WithReference(_ => 0));
    }

    [Fact]
    public void Abi_HasSixExports_FourTypedKeys_AndX64Layout()
    {
        Assert.Equal(136, Marshal.SizeOf<NativeShimAbi.UsbRecord>());
        Assert.Equal(4, Marshal.OffsetOf<NativeShimAbi.UsbRecord>("Identifier").ToInt32());
        Assert.Equal(0x00010000u, NativeShimAbi.Version);
        Assert.Equal(6, NativeShimAbi.ExportNames.Count);
        Assert.DoesNotContain(NativeShimAbi.ExportNames, name => name.Contains("pair", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("service", StringComparison.OrdinalIgnoreCase) || name.Contains("plist", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["DeviceName", "ProductType", "ProductVersion", "BuildVersion"], Enum.GetNames<DeviceMetadataField>());
        Assert.DoesNotContain("DeviceClass", Enum.GetNames<DeviceMetadataField>());
    }

    [Theory]
    [InlineData(1, NativeDeviceError.InvalidResponse)]
    [InlineData(2, NativeDeviceError.TransportUnavailable)]
    [InlineData(3, NativeDeviceError.NotTrusted)]
    [InlineData(4, NativeDeviceError.Restricted)]
    [InlineData(5, NativeDeviceError.InvalidResponse)]
    [InlineData(6, NativeDeviceError.InvalidResponse)]
    [InlineData(7, NativeDeviceError.Timeout)]
    [InlineData(8, NativeDeviceError.Disconnected)]
    [InlineData(9, NativeDeviceError.Unknown)]
    [InlineData(999, NativeDeviceError.InvalidResponse)]
    public void NativeErrors_AreMappedWithoutPrivateData(int code, object expected)
    {
        var exception = Assert.Throws<NativeDeviceException>(() => NativeShimAbi.Check((ShimError)code));
        Assert.Equal((NativeDeviceError)expected, exception.Error);
        Assert.Equal("Device operation unavailable.", exception.Message);
        NativeShimAbi.Check(ShimError.Success);
    }

    [Fact]
    public void MetadataDecoder_RejectsOversize_Unterminated_EmbeddedNull_InvalidUtf8()
    {
        var bytes = new byte[1025];
        Encoding.UTF8.GetBytes(new string('a', 256)).CopyTo(bytes, 0);
        Assert.Equal(256, NativeShimAbi.DecodeText(bytes, 256).Length);
        bytes[256] = (byte)'a';
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(bytes, 257));
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(bytes, 1025));
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(bytes, 255));
        bytes[10] = 0;
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(bytes, 256));
        Array.Clear(bytes); bytes[0] = 0xc0; bytes[1] = 0xaf;
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(bytes, 2));
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeText(new byte[1024], 0));
    }

    [Fact]
    public void UsbDecoder_EnforcesCount_IdentifierAndDuplicateBounds()
    {
        NativeShimAbi.UsbRecord Record(uint id, string text) => new()
        { Id = id, Identifier = Encoding.ASCII.GetBytes(text + "\0").Concat(new byte[128 - text.Length]).ToArray() };
        var records = Enumerable.Range(1, 32).Select(index => Record((uint)index, $"synthetic-{index}")).ToArray();
        Assert.Equal(32, NativeShimAbi.DecodeDevices(records, 32).Count);
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeDevices(records, 33));
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeDevices(records, 999));
        records[1] = records[0];
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeDevices(records, 2));
        records[0] = Record(1, "bad/path");
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeDevices(records, 1));
        records[0] = new() { Id = 1, Identifier = Enumerable.Repeat((byte)'a', 129).ToArray() };
        Assert.Throws<NativeDeviceException>(() => NativeShimAbi.DecodeDevices(records, 1));
    }

    [Fact]
    public void SafeHandles_PreserveParentsAndReleaseOnce_AfterInFlightWork()
    {
        var releases = new List<int>();
        var parent = new NativeOwnershipHandle(new IntPtr(1), pointer => releases.Add(pointer.ToInt32()));
        var child = new NativeOwnershipHandle(new IntPtr(2), pointer => releases.Add(pointer.ToInt32()), parent);
        parent.Dispose();
        Assert.Empty(releases);
        child.WithReference(pointer => { child.Dispose(); Assert.Empty(releases); return 0; });
        Assert.Equal([2, 1], releases);
        child.Dispose(); parent.Dispose();
        Assert.Equal([2, 1], releases);
        Assert.Throws<ObjectDisposedException>(() => child.WithReference(_ => 0));
    }

    [Fact]
    public void SafeHandle_ExceptionDuringWork_DoesNotLoseOwnership()
    {
        var releases = 0;
        using (var handle = new NativeOwnershipHandle(new IntPtr(1), _ => releases++))
            Assert.Throws<InvalidOperationException>(() => handle.WithReference<int>(_ => throw new InvalidOperationException()));
        Assert.Equal(1, releases);
    }

    [Fact]
    public void Validator_HoldsFiles_AndRejectsWrongHash_ExtraFile_MissingFile()
    {
        using var fixture = new Bundle();
        using (fixture.Validate())
        {
            Assert.Throws<IOException>(() => File.Open(fixture.Path("libplist-2.0.dll"), FileMode.Open, FileAccess.Write).Dispose());
            Assert.Throws<IOException>(() => Directory.Move(fixture.Root, fixture.Root + "-moved"));
        }
        File.WriteAllBytes(fixture.Path("libplist-2.0.dll"), new byte[4608]);
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validate());
        fixture.Restore();
        File.WriteAllText(fixture.Path("extra.txt"), "synthetic");
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validate());
        File.Delete(fixture.Path("extra.txt"));
        File.Delete(fixture.Path("libplist-2.0.dll"));
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validate());
    }

    [Fact]
    public void Validator_RejectsWrongDependency_EvenWhenHashMatches()
    {
        using var fixture = new Bundle("UNREVIEWED.dll");
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validate());
    }

    [Theory]
    [InlineData(Architecture.X86, null)]
    [InlineData(Architecture.Arm64, null)]
    [InlineData(Architecture.X64, "")]
    [InlineData(Architecture.X64, "127.0.0.1:27015")]
    [InlineData(Architecture.X64, "remote:27015")]
    public void Validator_RejectsArchitectureAndAllUserOverrides(Architecture architecture, string? endpoint)
    {
        using var fixture = new Bundle();
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validator.Validate(fixture.Root, architecture, endpoint));
    }

    [Fact]
    public void Validator_RejectsTraversal_NetworkAndReparseFiles()
    {
        using var fixture = new Bundle();
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validator.Validate(fixture.Root + "\\..\\" +
            System.IO.Path.GetFileName(fixture.Root), Architecture.X64, null));
        Assert.Throws<NativeRuntimeRejectedException>(() => fixture.Validator.Validate(@"\\server\share", Architecture.X64, null));
        Assert.Throws<NativeRuntimeRejectedException>(() => new NativeRuntimeValidator(fixture.Pins, new Paths(true, false))
            .Validate(fixture.Root, Architecture.X64, null));
        Assert.Throws<NativeRuntimeRejectedException>(() => new NativeRuntimeValidator(fixture.Pins, new Paths(false, true))
            .Validate(fixture.Root, Architecture.X64, null));
    }

    [Fact]
    public void PeInspector_RejectsWrongArchitecture_DelayImports_Forwarders_AndMalformedImages()
    {
        byte[] Image() => Bundle.Pe("KERNEL32.dll");
        var bytes = Image();
        Assert.Equal(["KERNEL32.dll"], NativePeImage.Read(new MemoryStream(bytes)).Imports);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84, 2), 0x014c);
        Assert.ThrowsAny<Exception>(() => NativePeImage.Read(new MemoryStream(bytes)));
        bytes = Image(); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 112 + 13 * 8, 4), 0x1000);
        Assert.ThrowsAny<Exception>(() => NativePeImage.Read(new MemoryStream(bytes)));
        bytes = Image(); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x380, 4), 0x1101);
        Assert.ThrowsAny<Exception>(() => NativePeImage.Read(new MemoryStream(bytes)));
        Assert.ThrowsAny<Exception>(() => NativePeImage.Read(new MemoryStream(new byte[2])));
    }

    private sealed class Paths(bool network, bool reparse) : INativePathInspector
    {
        public bool IsNetworkDrive(string root) => network;
        public FileAttributes? GetAttributes(string path) => reparse && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? FileAttributes.ReparsePoint : File.GetAttributes(path);
    }

    private sealed class Bundle : IDisposable
    {
        private readonly ImageTestFiles files = new();
        private readonly byte[] bytes;
        internal string Root { get; }
        internal IReadOnlyList<NativeFilePin> Pins { get; }
        internal NativeRuntimeValidator Validator => new(Pins);
        internal Bundle(string dependency = "KERNEL32.dll")
        {
            Root = files.PathFor("bundle");
            Directory.CreateDirectory(System.IO.Path.Combine(Root, "native", "win-x64"));
            bytes = Pe(dependency);
            Pins = new[] { "libcrypto-3-x64.dll", "libssl-3-x64.dll", "libplist-2.0.dll", "cardryft-device.dll" }
                .Select(name => new NativeFilePin(name, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), ["KERNEL32.dll"]))
                .ToArray();
            Restore();
        }
        internal string Path(string name) => System.IO.Path.Combine(Root, "native", "win-x64", name);
        internal void Restore() { foreach (var pin in Pins) File.WriteAllBytes(Path(pin.Name), bytes); }
        internal ValidatedNativeBundle Validate() => Validator.Validate(Root, Architecture.X64, null);
        public void Dispose() => files.Dispose();

        // Small synthetic AMD64 PE. It is parsed only, never loaded or executed.
        internal static byte[] Pe(string dependency)
        {
            var bytes = new byte[4608];
            void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
            void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
            void Text(int offset, string text) => Encoding.ASCII.GetBytes(text + "\0").CopyTo(bytes, offset);
            U16(0, 0x5a4d); U32(0x3c, 0x80); U32(0x80, 0x4550);
            U16(0x84, 0x8664); U16(0x86, 1); U16(0x94, 240); U16(0x96, 0x2022);
            const int optional = 0x98;
            U16(optional, 0x20b); U32(optional + 32, 4096); U32(optional + 36, 512);
            U32(optional + 56, 12288); U32(optional + 60, 512); U16(optional + 68, 3); U32(optional + 108, 16);
            U32(optional + 112, 0x1100); U32(optional + 116, 256);
            U32(optional + 120, 0x1000); U32(optional + 124, 40);
            const int section = optional + 240;
            Text(section, ".rdata"); U32(section + 8, 4096); U32(section + 12, 0x1000);
            U32(section + 16, 4096); U32(section + 20, 512); U32(section + 36, 0x40000040);
            U32(0x200 + 12, 0x1060); Text(0x260, dependency);
            U32(0x300 + 20, 6); U32(0x300 + 24, 6); U32(0x300 + 28, 0x1180); U32(0x300 + 32, 0x11c0);
            var cursor = 0x420;
            for (var index = 0; index < 6; index++)
            {
                U32(0x380 + index * 4, 0x2000); U32(0x3c0 + index * 4, (uint)(cursor - 0x200 + 0x1000));
                Text(cursor, NativeShimAbi.ExportNames[index]); cursor += NativeShimAbi.ExportNames[index].Length + 1;
            }
            return bytes;
        }
    }
}
