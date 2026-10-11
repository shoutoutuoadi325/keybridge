using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace KeyBridge.App;

public partial class App : Application
{
    public static double TextScale { get; private set; } = 1;
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { TextScale = Math.Clamp(new Windows.UI.ViewManagement.UISettings().TextScaleFactor, 1, 2.25); }
        catch (Exception) { TextScale = Math.Max(1, SystemFonts.MessageFontSize / 12); }
        Resources["AppFontSize"] = 14 * TextScale;
        Resources["TitleFontSize"] = 30 * TextScale;
        Resources["BrandFontSize"] = 27 * TextScale;
        var smoke = Array.IndexOf(e.Args, "--smoke-test") >= 0;
        instance = new Mutex(true, "Local\\KeyBridge." + Environment.UserName, out var first);
        if (!first && !smoke)
        {
            MessageBox.Show("KeyBridge is already running. Open it from the notification area.", "KeyBridge");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            (MainWindow as MainWindow)?.EmergencyStop("An unexpected error occurred. Mapping has stopped.");
            args.Handled = true;
            MessageBox.Show("KeyBridge stopped mapping after an unexpected error. Restart the app and release any held modifiers.", "KeyBridge");
            Shutdown(1);
        };
        var window = new MainWindow(smoke);
        MainWindow = window;
        window.Show();
        if (smoke)
        {
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try { await window.RenderSmokeAsync(Path.GetFullPath("artifacts/ui-smoke")); Shutdown(); }
                catch (Exception ex) { File.WriteAllText("smoke-error.txt", ex.ToString()); Shutdown(1); }
            }));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (MainWindow as MainWindow)?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
