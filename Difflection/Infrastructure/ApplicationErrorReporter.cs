using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Difflection.Infrastructure;

public static class ApplicationErrorReporter
{
    private static readonly Lock LogLock = new();

    private static string LogFilePath
    {
        get
        {
            var rootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Difflection",
                "logs");

            return Path.Combine(rootPath, "difflection.log");
        }
    }

    public static void Report(Exception exception, string message, bool showDialog = true)
    {
        WriteLog(exception, message);

        if (showDialog && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            Dispatcher.UIThread.Post(() => ShowDialog(message, exception));
        }
    }

    public static void WriteLog(Exception exception, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

            var entry = string.Join(
                Environment.NewLine,
                $"[{DateTimeOffset.Now:O}] {message}",
                exception,
                string.Empty);

            lock (LogLock)
            {
                File.AppendAllText(LogFilePath, entry);
            }
        }
        catch
        {
            // Logging must never become the reason the app fails.
        }
    }

    private static async void ShowDialog(string message, Exception exception)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
                || desktop.MainWindow is null)
            {
                return;
            }

            var dialog = CreateErrorDialog(message, exception);
            await dialog.ShowDialog(desktop.MainWindow);
        }
        catch (Exception dialogException)
        {
            WriteLog(dialogException, "Failed to show error dialog.");
        }
    }

    private static Window CreateErrorDialog(string message, Exception exception)
    {
        var closeButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 84
        };

        var dialog = new Window
        {
            Title = "Difflection Error",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = $"Details were written to:{Environment.NewLine}{LogFilePath}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = exception.Message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Opacity = 0.78
                    },
                    closeButton
                }
            }
        };

        closeButton.Click += (_, _) => dialog.Close();
        return dialog;
    }
}
