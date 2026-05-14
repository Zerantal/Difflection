using Avalonia;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    private async void ProjectListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ProjectListItemViewModel { IsEditing: true } row })
        {
            await _viewModel.Workspace.CommitProjectRenameAsync(row);
        }
    }

    private async void ProjectListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ProjectListItemViewModel row } || !row.IsEditing)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.Workspace.CommitProjectRenameAsync(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            WorkspaceNavigatorViewModel.CancelProjectRename(row);
            e.Handled = true;
        }
    }

    private async void ComparisonListNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is TextBox { DataContext: ComparisonListItemViewModel { IsEditing: true } row })
        {
            await _viewModel.Workspace.CommitComparisonRenameAsync(row);
        }
    }

    private async void ComparisonListNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ComparisonListItemViewModel row } || !row.IsEditing)
        {
            return;
        }

        if (e.Key == Key.Enter && _viewModel is not null)
        {
            await _viewModel.Workspace.CommitComparisonRenameAsync(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            WorkspaceNavigatorViewModel.CancelComparisonRename(row);
            e.Handled = true;
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

    private void InlineNameTextBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        FocusInlineNameEditor(sender as TextBox);
    }

    private void InlineNameTextBox_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            FocusInlineNameEditor(sender as TextBox);
        }
    }

    private static void FocusInlineNameEditor(TextBox? textBox)
    {
        if (textBox is not null
            && textBox.IsVisible
            && (textBox.DataContext is ProjectListItemViewModel { IsEditing: true }
                || textBox.DataContext is ComparisonListItemViewModel { IsEditing: true }))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!textBox.IsVisible)
                {
                    return;
                }

                textBox.Focus();
                textBox.SelectAll();
            });
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainWindowViewModel;
    }
}
