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
    private bool shuttingDown;
    private bool allowClose;
    private bool leavePromptOpen;
    private readonly ToolStripMenuItem fileMenu = new("File");
    private readonly ToolStripMenuItem recentMenu = new("Recent Projects");
    private readonly ToolStripMenuItem saveItem = new("Save");
    private readonly ToolStripMenuItem saveAsItem = new("Save As...");
    private readonly ToolStripMenuItem undoItem = new("Undo");
    private readonly ToolStripMenuItem redoItem = new("Redo");
    private readonly Button import = CreateButton("Import Image");

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

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 0, 0), WrapContents = false };
        toolbar.Controls.Add(import);
        toolbar.Controls.Add(export);
        var dimensions = CreateLabel($"{ArtworkSize.Canonical.Width} × {ArtworkSize.Canonical.Height} · PNG artwork");
        dimensions.Margin = new Padding(20, 9, 0, 0);
        toolbar.Controls.Add(dimensions);
        layout.Controls.Add(toolbar, 0, 1);

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
        layout.Controls.Add(workspace, 0, 2);
        status.Dock = DockStyle.Fill;
        status.AutoSize = false;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.Padding = new Padding(16, 0, 0, 0);
        status.AutoEllipsis = true;
        layout.Controls.Add(status, 0, 3);
        Controls.Add(layout);
        var menu = CreateMenu();
        layout.Controls.Add(menu, 0, 0);
        MainMenuStrip = menu;

        import.Click += async (_, _) => await ImportImageAsync();
        export.Click += async (_, _) => await ExportImageAsync();
        reset.Click += async (_, _) => await RunEditorActionAsync(() => { editor.Reset(); return Task.CompletedTask; }, "Transform reset.");
        zoom.ValueChanged += (_, _) => ChangeTransform();
        horizontal.ValueChanged += (_, _) => ChangeTransform();
        vertical.ValueChanged += (_, _) => ChangeTransform();
        editor.Changed += SynchronizeControls;
        editor.RenderFailed += ShowFailure;
        FormClosing += OnFormClosing;
        AllowDrop = preview.AllowDrop = true;
        DragEnter += OnDragEnter;
        preview.DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        preview.DragDrop += OnDragDrop;
        SynchronizeControls();
    }

    private MenuStrip CreateMenu()
    {
        var menu = new MenuStrip { Dock = DockStyle.Fill, AccessibleName = "Main menu" };
        var newItem = new ToolStripMenuItem("New", null, async (_, _) =>
        { if (ConfirmLeave()) await RunEditorActionAsync(editor.NewAsync, "New project."); }) { ShortcutKeys = Keys.Control | Keys.N };
        var openItem = new ToolStripMenuItem("Open...", null, async (_, _) => await OpenProjectAsync()) { ShortcutKeys = Keys.Control | Keys.O };
        saveItem.ShortcutKeys = Keys.Control | Keys.S;
        saveAsItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        saveItem.Click += (_, _) => SaveProject(false);
        saveAsItem.Click += (_, _) => SaveProject(true);
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Close());
        fileMenu.DropDownItems.AddRange([newItem, openItem, saveItem, saveAsItem, recentMenu, new ToolStripSeparator(), exitItem]);
        fileMenu.DropDownOpening += (_, _) => RefreshRecent();
        var edit = new ToolStripMenuItem("Edit");
        undoItem.ShortcutKeys = Keys.Control | Keys.Z;
        redoItem.ShortcutKeys = Keys.Control | Keys.Y;
        undoItem.Click += (_, _) => { editor.Undo(); status.Text = "Undo."; };
        redoItem.Click += (_, _) => { editor.Redo(); status.Text = "Redo."; };
        edit.DropDownItems.AddRange([undoItem, redoItem]);
        menu.Items.AddRange([fileMenu, edit]);
        return menu;
    }

    private void RefreshRecent()
    {
        recentMenu.DropDownItems.Clear();
        foreach (var path in editor.RecentPaths)
        {
            var item = new ToolStripMenuItem(Path.GetFileName(path)) { ToolTipText = path };
            item.Click += async (_, _) =>
            { if (ConfirmLeave()) await RunEditorActionAsync(() => editor.OpenAsync(path), "Project opened."); };
            recentMenu.DropDownItems.Add(item);
        }
        recentMenu.Enabled = recentMenu.DropDownItems.Count > 0;
    }

    private async Task OpenProjectAsync()
    {
        if (!ConfirmLeave()) return;
        using var dialog = new OpenFileDialog { Title = "Open Cardryft project", Filter = "Cardryft projects|*.cardryft", CheckFileExists = true, RestoreDirectory = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunEditorActionAsync(() => editor.OpenAsync(dialog.FileName), "Project opened.");
    }

    private bool SaveProject(bool saveAs)
    {
        var path = editor.Document.ProjectPath;
        if (editor.Session is null || editor.IsBusy) return false;
        if (saveAs || path is null)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save Cardryft project", Filter = "Cardryft projects|*.cardryft", DefaultExt = "cardryft",
                FileName = path is null ? "Untitled.cardryft" : Path.GetFileName(path),
                AddExtension = true, OverwritePrompt = true, RestoreDirectory = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try { editor.Save(path); status.Text = editor.Warning ?? "Project saved."; return true; }
        catch (Exception exception) when (ArtworkEditor.IsFileError(exception)) { ShowFailure(exception); return false; }
    }

    private bool ConfirmLeave()
    {
        if (editor.IsBusy || shuttingDown || leavePromptOpen) return false;
        if (!editor.Document.IsDirty) return true;
        using var dialog = new Form { Text = "Unsaved changes", ClientSize = new Size(390, 125),
            FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false };
        var label = CreateLabel("Save changes to the current project?");
        label.Location = new Point(18, 18);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        var cancel = CreateButton("Cancel"); cancel.DialogResult = DialogResult.Cancel;
        var discard = CreateButton("Discard"); discard.DialogResult = DialogResult.No;
        var save = CreateButton("Save"); save.DialogResult = DialogResult.Yes;
        buttons.Controls.AddRange([cancel, discard, save]);
        dialog.Controls.AddRange([label, buttons]);
        dialog.AcceptButton = save; dialog.CancelButton = cancel;
        leavePromptOpen = true;
        try
        {
            return dialog.ShowDialog(this) switch
            { DialogResult.No => true, DialogResult.Yes => SaveProject(false), _ => false };
        }
        finally { leavePromptOpen = false; }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (shuttingDown) return;
        if (editor.IsBusy) { status.Text = "Wait for the current file operation, then close again."; return; }
        if (!ConfirmLeave()) return;
        shuttingDown = true;
        Enabled = false;
        try { await editor.ShutdownAsync(); }
        catch (Exception exception) when (ArtworkEditor.IsFileError(exception)) { }
        editor.Changed -= SynchronizeControls;
        editor.RenderFailed -= ShowFailure;
        allowClose = true;
        BeginInvoke(new Action(Close));
    }

    private static string[] DroppedFiles(IDataObject? data) => data?.GetData(DataFormats.FileDrop) as string[] ?? [];

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (editor.IsBusy || shuttingDown) return;
        if (DroppedFiles(e.Data).Length > 0) e.Effect = DragDropEffects.Copy;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (editor.IsBusy || shuttingDown) return;
        if (editor.HasSource && !ConfirmLeave()) return;
        await RunEditorActionAsync(() => editor.ImportAsync(ImageDrop.Validate(DroppedFiles(e.Data))), "Dropped image imported.");
    }

    private async Task ImportImageAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import artwork image", Filter = "PNG and JPEG images|*.png;*.jpg;*.jpeg",
            CheckFileExists = true, Multiselect = false, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (editor.HasSource && !ConfirmLeave()) return;
        await RunEditorActionAsync(() => editor.ImportAsync(dialog.FileName), $"Imported {Path.GetFileName(dialog.FileName)}.");
    }

    private async Task ExportImageAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export artwork PNG", Filter = "PNG artwork|*.png", DefaultExt = "png",
            AddExtension = true, FileName = "cardryft-artwork.png", OverwritePrompt = true, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunEditorActionAsync(() => editor.ExportAsync(dialog.FileName), $"Exported {Path.GetFileName(dialog.FileName)}.");
    }

    private void ChangeTransform()
    {
        if (synchronizing || editor.Session is null) return;
        editor.SetTransform(new ArtworkTransform(zoom.Value / 100d,
            horizontal.Value / 100d, vertical.Value / 100d));
        status.Text = "Transform updated.";
    }

    private async Task RunEditorActionAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            status.Text = editor.Warning ?? successMessage;
            if (editor.Warning is not null && !shuttingDown)
                MessageBox.Show(this, editor.Warning, "Cardryft", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) when (shuttingDown) { }
        catch (Exception exception) when (ArtworkEditor.IsFileError(exception))
        {
            if (!shuttingDown) ShowFailure(exception);
        }
        finally
        {
            SynchronizeControls();
        }
    }

    private void ShowFailure(Exception exception)
    {
        status.Text = "Operation failed. Current project state was preserved.";
        MessageBox.Show(this, "The file could not be read, rendered, or saved. Choose a valid local .cardryft project " +
            "or PNG/JPEG image (25 MiB, 8192 pixels per side, 32 million pixels maximum). Check the destination is writable.",
            "Cardryft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var loaded = editor.Session is not null && !editor.IsBusy;
            zoom.Enabled = horizontal.Enabled = vertical.Enabled = reset.Enabled = loaded;
            export.Enabled = loaded && editor.HasSource;
            import.Enabled = fileMenu.Enabled = !editor.IsBusy && !shuttingDown;
            saveItem.Enabled = saveAsItem.Enabled = loaded;
            undoItem.Enabled = loaded && editor.Document.CanUndo;
            redoItem.Enabled = loaded && editor.Document.CanRedo;
            Text = $"{(editor.Document.IsDirty ? "* " : "")}{Path.GetFileName(editor.Document.ProjectPath) ?? "Untitled"} - Cardryft";
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
