using System.Runtime.InteropServices;
using Cardryft.Core;

namespace Cardryft.App;

internal sealed class MainForm : Form
{
    private readonly ArtworkEditor editor;
    private readonly ArtworkPreview preview = new() { Dock = DockStyle.Fill };
    private readonly TrackBar zoom = CreateSlider("Zoom", 100, 400, 100);
    private readonly TrackBar horizontal = CreateSlider("Horizontal position", -100, 100, 0);
    private readonly TrackBar vertical = CreateSlider("Vertical position", -100, 100, 0);
    private readonly Label zoomValue = CreateLabel("100%");
    private readonly Label horizontalValue = CreateLabel("0%");
    private readonly Label verticalValue = CreateLabel("0%");
    private readonly Label status = CreateLabel("Import a local PNG or JPEG. Artwork stays on this computer.");
    private readonly Button reset = CreateButton("Reset");
    private readonly Button export = CreateButton("Export PNG");
    private bool synchronizing;

    public MainForm(ArtworkEditor editor)
    {
        this.editor = editor;
        Text = "Cardryft";
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 0, 0), WrapContents = false };
        var import = CreateButton("Import Image");
        toolbar.Controls.Add(import);
        toolbar.Controls.Add(export);
        var dimensions = CreateLabel($"{ArtworkSize.Canonical.Width} × {ArtworkSize.Canonical.Height} · PNG artwork");
        dimensions.Margin = new Padding(20, 9, 0, 0);
        toolbar.Controls.Add(dimensions);
        layout.Controls.Add(toolbar, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        workspace.Controls.Add(preview, 0, 0);
        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(16, 22, 10, 10), AutoScroll = true,
        };
        AddSlider(controls, "Zoom", zoomValue, zoom);
        AddSlider(controls, "Horizontal position", horizontalValue, horizontal);
        AddSlider(controls, "Vertical position", verticalValue, vertical);
        reset.Margin = new Padding(0, 14, 0, 0);
        controls.Controls.Add(reset);
        var hint = CreateLabel("Pan follows the available crop.\nReset restores centered cover.");
        hint.ForeColor = Color.DimGray;
        hint.Margin = new Padding(0, 18, 0, 0);
        controls.Controls.Add(hint);
        workspace.Controls.Add(controls, 1, 0);
        layout.Controls.Add(workspace, 0, 1);
        status.Dock = DockStyle.Fill;
        status.AutoSize = false;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.Padding = new Padding(16, 0, 0, 0);
        status.AutoEllipsis = true;
        layout.Controls.Add(status, 0, 2);
        Controls.Add(layout);

        import.Click += (_, _) => ImportImage();
        export.Click += (_, _) => ExportImage();
        reset.Click += (_, _) => RunEditorAction(editor.Reset, "Transform reset.");
        zoom.ValueChanged += (_, _) => ChangeTransform();
        horizontal.ValueChanged += (_, _) => ChangeTransform();
        vertical.ValueChanged += (_, _) => ChangeTransform();
        SynchronizeControls();
    }

    private void ImportImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import artwork image", Filter = "PNG and JPEG images|*.png;*.jpg;*.jpeg",
            CheckFileExists = true, Multiselect = false, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        RunEditorAction(() => editor.Import(dialog.FileName), $"Imported {Path.GetFileName(dialog.FileName)}.");
    }

    private void ExportImage()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export artwork PNG", Filter = "PNG artwork|*.png", DefaultExt = "png",
            AddExtension = true, FileName = "cardryft-artwork.png", OverwritePrompt = true, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        RunEditorAction(() => editor.Export(dialog.FileName), $"Exported {Path.GetFileName(dialog.FileName)}.");
    }

    private void ChangeTransform()
    {
        if (synchronizing || editor.Session is null) return;
        RunEditorAction(() => editor.SetTransform(new ArtworkTransform(zoom.Value / 100d,
            horizontal.Value / 100d, vertical.Value / 100d)), "Preview updated.");
    }

    private void RunEditorAction(Action action, string successMessage)
    {
        try
        {
            action();
            status.Text = successMessage;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or ExternalException or OutOfMemoryException or NotSupportedException)
        {
            status.Text = "The operation failed. Your current artwork was preserved.";
            MessageBox.Show(this,
                "The file could not be read, rendered, or saved. Use a local PNG or JPEG up to 25 MiB, " +
                "8192 pixels per side and 32 million pixels total. For export, choose a writable local PNG path.",
                "Cardryft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SynchronizeControls();
        }
    }

    private void SynchronizeControls()
    {
        synchronizing = true;
        try
        {
            var transform = editor.Session?.Transform ?? ArtworkTransform.Default;
            zoom.Value = (int)Math.Round(transform.Zoom * 100);
            horizontal.Value = (int)Math.Round(transform.HorizontalOffset * 100);
            vertical.Value = (int)Math.Round(transform.VerticalOffset * 100);
            zoomValue.Text = $"{zoom.Value}%";
            horizontalValue.Text = $"{horizontal.Value}%";
            verticalValue.Text = $"{vertical.Value}%";
            var loaded = editor.Session is not null;
            zoom.Enabled = horizontal.Enabled = vertical.Enabled = reset.Enabled = export.Enabled = loaded;
            preview.Artwork = editor.Preview;
            preview.Invalidate();
        }
        finally { synchronizing = false; }
    }

    private static void AddSlider(Control parent, string title, Label value, TrackBar slider)
    {
        var heading = CreateLabel(title);
        heading.Margin = new Padding(0, 8, 0, 0);
        parent.Controls.Add(heading);
        parent.Controls.Add(value);
        parent.Controls.Add(slider);
    }

    private static TrackBar CreateSlider(string name, int minimum, int maximum, int value) => new()
    {
        AccessibleName = name, Minimum = minimum, Maximum = maximum, Value = value,
        TickFrequency = 50, SmallChange = 5, LargeChange = 25, Width = 185, Margin = new Padding(0, 0, 0, 10),
    };

    private static Label CreateLabel(string text) => new() { Text = text, AutoSize = true };

    private static Button CreateButton(string text) => new()
    {
        Text = text, AccessibleName = text, AutoSize = true, Height = 34,
        Padding = new Padding(8, 2, 8, 2), UseVisualStyleBackColor = true,
    };
}
