using System.Diagnostics;
using System.Windows.Input;

namespace Sona.App;

public static class RepositoryLinks
{
    public static RoutedUICommand Open { get; } = new("Open Sona on GitHub", "OpenRepository", typeof(RepositoryLinks));
    public static Uri Repository { get; } = new("https://github.com/JhinxDev/Sona");
}

public partial class MainWindow
{
    internal Uri? LastRepositoryLinkForTests { get; private set; }
    private void OpenRepository(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (e.Parameter is not ("repository" or "star")) return;
        // Both actions open the repository. Starring remains the visitor's choice
        // on GitHub; no account, token or automated social action is used.
        if (arguments.Contains("--test-ui")) { LastRepositoryLinkForTests = RepositoryLinks.Repository; return; }
        try { Process.Start(new ProcessStartInfo(RepositoryLinks.Repository.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { StatusDetail.Text = "Could not open GitHub. Visit https://github.com/JhinxDev/Sona in your browser."; }
    }
}
