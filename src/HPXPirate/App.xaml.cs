using System.Windows;
using System.Windows.Threading;
using HPXPirate.Services;

namespace HPXPirate;

/// <summary>
/// Application entry point. Wires up crash protection, logging and the main window.
/// </summary>
public partial class App : Application
{
    public static AppHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ---- Crash protection -------------------------------------------------
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // ---- Bootstrap services ----------------------------------------------
        Host = new AppHost();

        try
        {
            Host.Initialize();
            Host.Logger.Info("HPX PIRATE starting up.");

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Host.Logger.Critical("Fatal error during startup.", ex);
            MessageBox.Show(
                "HPX PIRATE failed to start.\n\n" + ex.Message +
                "\n\nNo system changes were applied. Details were written to the log file.",
                "HPX PIRATE - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Host?.Logger?.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show(
            "An unexpected error occurred but HPX PIRATE kept running.\n\n" + e.Exception.Message +
            "\n\nThe error was logged. No system changes were applied.",
            "HPX PIRATE - Recovered", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // keep the app alive
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Host?.Logger?.Critical("Unhandled domain exception.", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Host?.Logger?.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Host?.Shutdown();
        }
        catch (Exception ex)
        {
            Host?.Logger?.Error("Error during shutdown.", ex);
        }
        base.OnExit(e);
    }
}
