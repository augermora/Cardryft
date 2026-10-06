namespace Cardryft.App;

internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Cardryft";
        ClientSize = new Size(640, 360);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Cardryft\nSolution foundation",
            AccessibleName = "Cardryft solution foundation",
        });
    }
}
