using Cardryft.Imaging;
using Cardryft.Storage;

namespace Cardryft.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(),
            new ProjectStore(), new RecentProjects());
        using var form = new MainForm(editor);
        Application.Run(form);
    }
}
