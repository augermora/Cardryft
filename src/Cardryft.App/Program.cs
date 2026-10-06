using Cardryft.Imaging;

namespace Cardryft.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer());
        using var form = new MainForm(editor);
        Application.Run(form);
    }
}
