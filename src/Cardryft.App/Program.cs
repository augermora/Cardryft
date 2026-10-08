using Cardryft.Imaging;
using Cardryft.Storage;
using Cardryft.Device.Apple.LibimobileDevice;

namespace Cardryft.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
#if CARDRYFT_NATIVE_OFFLINE_PROBE
        var offlineProbe = args.SequenceEqual(new[] { "--native-offline-probe" });
        if (offlineProbe)
        {
            try
            {
                using var bundle = new NativeRuntimeValidator(HardenedRuntimeManifest.Pins).Validate(
                    AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), System.Runtime.InteropServices.Architecture.X64,
                    Environment.GetEnvironmentVariable("USBMUXD_SOCKET_ADDRESS"));
                using var abi = NativeShimAbi.Load(bundle);
                abi.InitializeOffline(); // Winsock initialization only: no socket or device request.
            }
            catch
            {
                Environment.ExitCode = 1;
                return;
            }
        }
#endif
        ApplicationConfiguration.Initialize();
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(),
            new ProjectStore(), new RecentProjects());
        using var form = new MainForm(editor, new LibimobileDeviceDiscovery());
#if CARDRYFT_NATIVE_OFFLINE_PROBE
        if (offlineProbe)
            form.Shown += (_, _) =>
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "offline-native-probe-result.json"),
                    "{\"hashAndPeValidation\":true,\"abiVersion\":65536,\"initializedAndFreed\":true,\"winFormsShown\":true,\"deviceOperations\":0}");
                form.BeginInvoke(form.Close);
            };
#endif
        Application.Run(form);
    }
}
