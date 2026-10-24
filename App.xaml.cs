using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace AskCons;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject?.ToString() ?? "Unknown fatal error");
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            window.WindowState = WindowState.Maximized;
            window.Activate();
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            MessageBox.Show("AskCons could not open its main window.\n\n" + ex.Message +
                "\n\nDetails saved to: " + LogPath, "AskCons startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception.ToString());
        MessageBox.Show("AskCons encountered an error:\n" + e.Exception.Message +
            "\n\nDiagnostic log: " + LogPath, "AskCons error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AskCons", "startup-errors.log");
    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
        }
        catch { /* Logging must never prevent error reporting. */ }
    }
}
