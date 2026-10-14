using System.Windows;

namespace Sona.App;

/// <summary>Test-only application that proves WPF startup can be intercepted before production services or windows start.</summary>
internal sealed class ThemeResourceTestApplication : App
{
    public bool StartupCallbackObserved { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Deliberately do not call base: production startup creates the main window and initializes services.
        StartupCallbackObserved = true;
    }
}
