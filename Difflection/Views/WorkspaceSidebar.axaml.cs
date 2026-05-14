using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Difflection.Infrastructure;
using Difflection.ViewModels;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace Difflection.Views;

public partial class WorkspaceSidebar : UserControl
{
    private MainWindowViewModel? _viewModel;

    public WorkspaceSidebar()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public Func<string, string, Task<bool>> ConfirmDestructiveActionAsync { get; set; } = ConfirmationDialogService.ShowAsync;

    public Action? RequestImageChangeMonitorRestart { get; set; }

    private void SidebarItem_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { ContextMenu: { Items: { } items } } control || control.DataContext is null)
        {
            return;
        }

        foreach (var menuItem in items.OfType<MenuItem>())
        {
            menuItem.CommandParameter = control.DataContext;

            if (string.Equals(menuItem.Header?.ToString(), "Rename", StringComparison.Ordinal))
            {
                menuItem.Command = control.DataContext switch
                {
                    ProjectListItemViewModel => _viewModel?.Workspace.BeginRenameProjectCommand,
                    ComparisonListItemViewModel => _viewModel?.Workspace.BeginRenameComparisonCommand,
                    _ => menuItem.Command
                };
            }
        }
    }

    private async void RefreshComparisonSourcesMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ComparisonListItemViewModel row })
        {
            return;
        }

        await _viewModel.RefreshComparisonSourceImagesAsync(row.Comparison);
        RequestImageChangeMonitorRestart?.Invoke();
    }

    private async void DeleteComparisonMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem { CommandParameter: ComparisonListItemViewModel row })
        {
            return;
        }

        var confirmed = await ConfirmDestructiveActionAsync(
            "Delete comparison?",
            $"Delete comparison \"{row.Name}\" and all images in its image set?");

        if (!confirmed)
        {
            return;
        }

        await _viewModel.Workspace.DeleteComparisonAsync(row.Comparison);
        RequestImageChangeMonitorRestart?.Invoke();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainWindowViewModel;
    }
}
