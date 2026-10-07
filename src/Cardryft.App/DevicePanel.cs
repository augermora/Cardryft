using Cardryft.Core;

namespace Cardryft.App;

internal sealed class DevicePanel : FlowLayoutPanel
{
    private readonly DeviceRefreshController refresh;
    private readonly Label summary = new()
    {
        AutoSize = true, MaximumSize = new Size(185, 0),
        Text = "Native device backend pending validation. Click Refresh to check prerequisites.",
    };

    public DevicePanel(IDeviceDiscovery discovery)
    {
        refresh = new(discovery);
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoSize = true;
        Width = 185;
        Margin = new Padding(0, 18, 0, 0);
        Controls.Add(new Label { Text = "Device (USB only)", AutoSize = true });
        Controls.Add(summary);
        var button = new Button { Text = "Refresh", AccessibleName = "Refresh USB devices", AutoSize = true };
        button.Click += (_, _) => refresh.Refresh();
        Controls.Add(button);
        refresh.Changed += Present;
    }

    public Task StopAsync() => refresh.StopAsync();

    private void Present() => summary.Text = Describe(refresh.Current!);

    internal static string Describe(DeviceDiscoveryResult result)
    {
        if (result.Status == DeviceConnectionStatus.NativeDependencyUnavailable)
            return result.Diagnostic switch
            {
                DeviceDiagnostic.UnsupportedArchitecture => "Device access requires Windows x64. Artwork editing remains available.",
                DeviceDiagnostic.UnsafeNativePath => "Device access disabled: unsafe native library location. Artwork editing remains available.",
                DeviceDiagnostic.UnsafeTransportOverride => "Device access disabled: a USB transport override is configured. Artwork editing remains available.",
                DeviceDiagnostic.NativeLocationUnavailable => "Native library location could not be checked. Artwork editing remains available.",
                _ => "Native dependencies unavailable. Device detection is pending validation. Artwork editing remains available.",
            };
        if (result.Status == DeviceConnectionStatus.NoDevice) return "No USB device connected.";
        if (result.Devices.Count == 0) return $"Device query unavailable: {result.Status}.";
        return string.Join("\n\n", result.Devices.Select((device, index) =>
            $"Device {index + 1}\nStatus: {device.Status}\nName: {device.Name ?? "Unavailable"}\n" +
            $"Model: {device.ProductType ?? "Unavailable"}\niOS: {device.ProductVersion ?? "Unavailable"}\n" +
            $"Build: {device.BuildVersion ?? "Unavailable"}\nTrust: {device.Trust}"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) refresh.Changed -= Present;
        base.Dispose(disposing);
    }
}
