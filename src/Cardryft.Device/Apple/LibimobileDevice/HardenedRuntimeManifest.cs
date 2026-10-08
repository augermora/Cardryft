// Generated from a static build audit. This does not approve promotion or hardware use.
namespace Cardryft.Device.Apple.LibimobileDevice;

internal static class HardenedRuntimeManifest
{
    internal const bool PromotionApproved = false;
    internal static IReadOnlyList<NativeFilePin> Pins { get; } = Array.AsReadOnly(new NativeFilePin[]
    {
        new("cardryft-device.dll", 54226, "de3819264b2210ba2975984d22ea77a6e5e2f1d1b0b8a14fe1084cf2597427ae", Array.AsReadOnly(new[] { "libcrypto-3-x64.dll", "libplist-2.0.dll", "libssl-3-x64.dll", "KERNEL32.dll", "api-ms-win-crt-heap-l1-1-0.dll", "api-ms-win-crt-private-l1-1-0.dll", "api-ms-win-crt-runtime-l1-1-0.dll", "api-ms-win-crt-stdio-l1-1-0.dll", "api-ms-win-crt-string-l1-1-0.dll", "WS2_32.dll" })),
        new("libcrypto-3-x64.dll", 9031118, "e739523e963c313ad5f6c07275fe58ae5c8f2e3f8c2871096990b2cb14dfa2fd", Array.AsReadOnly(new[] { "ADVAPI32.dll", "bcrypt.dll", "CRYPT32.dll", "KERNEL32.dll", "api-ms-win-crt-convert-l1-1-0.dll", "api-ms-win-crt-environment-l1-1-0.dll", "api-ms-win-crt-filesystem-l1-1-0.dll", "api-ms-win-crt-heap-l1-1-0.dll", "api-ms-win-crt-private-l1-1-0.dll", "api-ms-win-crt-runtime-l1-1-0.dll", "api-ms-win-crt-stdio-l1-1-0.dll", "api-ms-win-crt-string-l1-1-0.dll", "api-ms-win-crt-time-l1-1-0.dll", "api-ms-win-crt-utility-l1-1-0.dll", "USER32.dll" })),
        new("libplist-2.0.dll", 182898, "f3624144540e9c6363e337e1be9a6e3ab64e0db5923071ce96bf1160bfc7ca67", Array.AsReadOnly(new[] { "KERNEL32.dll", "api-ms-win-crt-convert-l1-1-0.dll", "api-ms-win-crt-filesystem-l1-1-0.dll", "api-ms-win-crt-heap-l1-1-0.dll", "api-ms-win-crt-math-l1-1-0.dll", "api-ms-win-crt-private-l1-1-0.dll", "api-ms-win-crt-runtime-l1-1-0.dll", "api-ms-win-crt-stdio-l1-1-0.dll", "api-ms-win-crt-string-l1-1-0.dll", "api-ms-win-crt-time-l1-1-0.dll" })),
        new("libssl-3-x64.dll", 1186777, "9cf8cd67df7f3eddff3d145cb4513f9e9745d35974d147a1c8b3b0ae93cbef45", Array.AsReadOnly(new[] { "libcrypto-3-x64.dll", "KERNEL32.dll", "api-ms-win-crt-convert-l1-1-0.dll", "api-ms-win-crt-filesystem-l1-1-0.dll", "api-ms-win-crt-private-l1-1-0.dll", "api-ms-win-crt-runtime-l1-1-0.dll", "api-ms-win-crt-stdio-l1-1-0.dll", "api-ms-win-crt-string-l1-1-0.dll", "api-ms-win-crt-time-l1-1-0.dll", "api-ms-win-crt-utility-l1-1-0.dll" })),
    });
}
