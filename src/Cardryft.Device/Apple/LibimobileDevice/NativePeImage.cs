using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal sealed record NativePeImage(IReadOnlyList<string> Imports, IReadOnlyList<string> Exports)
{
    internal static NativePeImage Read(Stream stream)
    {
        if (stream.Length is <= 0 or > NativeRuntimeValidator.MaximumFileBytes) throw new NativeRuntimeRejectedException();
        using var reader = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var headers = reader.PEHeaders;
        var optional = headers.PEHeader;
        if (headers.CoffHeader.Machine != Machine.Amd64 || headers.CoffHeader.TimeDateStamp != 0 ||
            !headers.CoffHeader.Characteristics.HasFlag(Characteristics.Dll) || optional is null || optional.Magic != PEMagic.PE32Plus ||
            optional.DelayImportTableDirectory.Size != 0 || optional.DelayImportTableDirectory.RelativeVirtualAddress != 0 ||
            headers.SectionHeaders.Length is 0 or > 96) throw new NativeRuntimeRejectedException();
        byte[] At(int rva, int length)
        {
            if (rva <= 0 || length < 0 || length > 65536) throw new NativeRuntimeRejectedException();
            var block = reader.GetSectionData(rva);
            if (block.Length < length) throw new NativeRuntimeRejectedException();
            return block.GetContent(0, length).ToArray();
        }
        static int U32(byte[] bytes, int offset) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        string Name(int rva)
        {
            var block = reader.GetSectionData(rva);
            var bytes = block.GetContent(0, Math.Min(256, block.Length)).ToArray();
            var end = Array.IndexOf(bytes, (byte)0);
            if (end <= 0 || bytes.AsSpan(0, end).ContainsAnyExceptInRange((byte)33, (byte)126)) throw new NativeRuntimeRejectedException();
            var name = Encoding.ASCII.GetString(bytes, 0, end);
            if (name.Any(character => character is '/' or '\\' or ':')) throw new NativeRuntimeRejectedException();
            return name;
        }
        var imports = new List<string>();
        var importTable = optional.ImportTableDirectory;
        if (importTable.RelativeVirtualAddress <= 0 || importTable.Size is < 20 or > 65536) throw new NativeRuntimeRejectedException();
        var descriptors = At(importTable.RelativeVirtualAddress, importTable.Size);
        var terminated = false;
        for (var offset = 0; offset + 20 <= descriptors.Length; offset += 20)
        {
            var descriptor = descriptors.AsSpan(offset, 20);
            if (descriptor.IndexOfAnyExcept((byte)0) < 0) { terminated = true; break; }
            if (imports.Count >= 64) throw new NativeRuntimeRejectedException();
            imports.Add(Name(U32(descriptors, offset + 12)));
        }
        if (!terminated || imports.Distinct(StringComparer.OrdinalIgnoreCase).Count() != imports.Count)
            throw new NativeRuntimeRejectedException();
        var exports = new List<string>();
        var exportTable = optional.ExportTableDirectory;
        if (exportTable.RelativeVirtualAddress <= 0 || exportTable.Size is < 40 or > 1048576) throw new NativeRuntimeRejectedException();
        var export = At(exportTable.RelativeVirtualAddress, 40);
        var functionCount = U32(export, 20);
        var nameCount = U32(export, 24);
        if (functionCount is <= 0 or > 16384 || nameCount != functionCount) throw new NativeRuntimeRejectedException();
        var functions = At(U32(export, 28), checked(functionCount * 4));
        var names = At(U32(export, 32), checked(nameCount * 4));
        for (var index = 0; index < functionCount; index++)
        {
            var function = U32(functions, index * 4);
            if (function == 0 || (function >= exportTable.RelativeVirtualAddress &&
                (long)function - exportTable.RelativeVirtualAddress < exportTable.Size)) throw new NativeRuntimeRejectedException();
            exports.Add(Name(U32(names, index * 4)));
        }
        if (exports.Distinct(StringComparer.Ordinal).Count() != exports.Count) throw new NativeRuntimeRejectedException();
        return new(imports, exports);
    }
}
