using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Difflection.Infrastructure;

public static class ConfirmationDialogService
{
    public static async Task<bool> ShowAsync(string title, string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return false;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = desktop.MainWindow;
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 84,
            IsCancel = true
        };
        cancelButton.Click += (_, _) => dialog.Close();

        var deleteButton = new Button
        {
            Content = "Delete",
            MinWidth = 84,
            Background = Brush.Parse("#7F1D1D"),
            Foreground = Brushes.White,
            BorderBrush = Brush.Parse("#B91C1C"),
            IsDefault = true
        };
        deleteButton.Click += (_, _) =>
        {
            completion.TrySetResult(true);
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeight.SemiBold
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children =
                    {
                        cancelButton,
                        deleteButton
                    }
                }
            }
        };

        dialog.Closed += DialogOnClosed;
        owner.IsEnabled = false;
        dialog.Show(owner);

        return await completion.Task;

        void DialogOnClosed(object? sender, EventArgs e)
        {
            dialog.Closed -= DialogOnClosed;
            owner.IsEnabled = true;
            owner.Activate();
            completion.TrySetResult(false);
        }
    }
}
