using Avalonia.Controls;
using Avalonia.Interactivity;
using Difflection.ViewModels;
using System;
using System.Threading.Tasks;

namespace Difflection.Views;

public partial class SettingsDialog : Window
{
    private TaskCompletionSource<bool>? _completion;

    public SettingsDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Refresh();
    }

    public bool MonitorSourceFilesForChanges => MonitorSourceFilesCheckBox.IsChecked == true;

    public Task<bool> ShowOwnedAsync(Window owner)
    {
        _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Closed += DialogOnClosed;
        Show(owner);
        return _completion.Task;
    }

    private void Refresh()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            MonitorSourceFilesCheckBox.IsChecked = viewModel.IsSelectedProjectSourceFileMonitoringEnabled;
            MonitorSourceFilesCheckBox.IsEnabled = viewModel.Workspace.HasSelectedProject;
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _completion?.TrySetResult(false);
        Close(false);
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _completion?.TrySetResult(true);
        Close(true);
    }

    private void DialogOnClosed(object? sender, EventArgs e)
    {
        Closed -= DialogOnClosed;
        _completion?.TrySetResult(false);
    }
}
