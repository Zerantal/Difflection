using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Difflection.Views;

public partial class ImageSetPanel : UserControl
{
    private bool _isImageSetExpanded = true;

    public ImageSetPanel()
    {
        InitializeComponent();

        UpdateImageSetExpandedState();
    }

    private void ToggleImageSetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _isImageSetExpanded = !_isImageSetExpanded;
        UpdateImageSetExpandedState();
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
