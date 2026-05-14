using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Difflection.ViewModels;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace Difflection.Views;

public partial class ImageSetPanel : UserControl
{
    private bool _isImageSetExpanded = true;

    public ImageSetPanel()
    {
        InitializeComponent();

        SizeChanged += OnSizeChanged;
        UpdateImageSetExpandedState();
    }

    private void ToggleImageSetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _isImageSetExpanded = !_isImageSetExpanded;
        UpdateImageSetExpandedState();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateImageSetHeightLimit();
    }

    private async void ImageLabelTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is TextBox { DataContext: ComparisonImageSetItemViewModel row } textBox)
        {
            await viewModel.ImageSet.LabelImageAsync(row.Image, textBox.Text);
        }
    }

    private async void ImageLabelTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainWindowViewModel viewModel || sender is not TextBox { DataContext: ComparisonImageSetItemViewModel row } textBox)
        {
            return;
        }

        await viewModel.ImageSet.LabelImageAsync(row.Image, textBox.Text);
        e.Handled = true;
    }

    private void UpdateImageSetHeightLimit()
    {
        if (ComparisonImagesList is null)
        {
            return;
        }

        ComparisonImagesList.ClearValue(MaxHeightProperty);
    }

    private void UpdateImageSetExpandedState()
    {
        if (ComparisonImagesList is null
            || DifferenceStatusTextBlock is null
            || PanelBorder is null
            || CollapseImageSetIcon is null
            || ExpandImageSetIcon is null
            || ToggleImageSetButton is null)
        {
            return;
        }

        ComparisonImagesList.IsVisible = _isImageSetExpanded;
        DifferenceStatusTextBlock.IsVisible = _isImageSetExpanded;
        PanelBorder.Padding = _isImageSetExpanded ? new Thickness(18, 12) : new Thickness(18, 8);
        CollapseImageSetIcon.IsVisible = _isImageSetExpanded;
        ExpandImageSetIcon.IsVisible = !_isImageSetExpanded;
        ToolTip.SetTip(ToggleImageSetButton, _isImageSetExpanded ? "Collapse image set" : "Expand image set");
    }
}
