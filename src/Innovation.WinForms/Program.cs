using System.Text;

namespace Innovation.WinForms;

/// <summary>
/// Entry point. Registers code-page support for the TSV card loader
/// (same workaround used throughout the test suite on net10.0), shows
/// the new-game dialog, then launches the main form.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        using var dialog = new NewGameDialog();
        if (dialog.ShowDialog() != DialogResult.OK) return;

        Application.Run(new GameForm(dialog.Config));
    }
}
