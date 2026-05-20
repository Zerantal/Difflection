using System.Collections;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Difflection.ViewModels;

namespace Difflection.Views;

public partial class ImageChannelFrame : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<ImageChannelFrame, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<string> ChannelNameProperty =
        AvaloniaProperty.Register<ImageChannelFrame, string>(nameof(ChannelName), string.Empty);

    public static readonly StyledProperty<string> ListNameProperty =
        AvaloniaProperty.Register<ImageChannelFrame, string>(nameof(ListName), string.Empty);

    public static readonly StyledProperty<bool> IsBaselineChannelProperty =
        AvaloniaProperty.Register<ImageChannelFrame, bool>(nameof(IsBaselineChannel));

    public static readonly StyledProperty<bool> IsCandidateChannelProperty =
        AvaloniaProperty.Register<ImageChannelFrame, bool>(nameof(IsCandidateChannel));

    public ImageChannelFrame()
    {
        InitializeComponent();
    }

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string ChannelName
    {
        get => GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }

    public string ListName
    {
        get => GetValue(ListNameProperty);
        set => SetValue(ListNameProperty, value);
    }

    public bool IsBaselineChannel
    {
        get => GetValue(IsBaselineChannelProperty);
        set => SetValue(IsBaselineChannelProperty, value);
    }

    public bool IsCandidateChannel
    {
        get => GetValue(IsCandidateChannelProperty);
        set => SetValue(IsCandidateChannelProperty, value);
    }

    private async void TagImageMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not MenuItem { DataContext: ComparisonImageSetItemViewModel row })
        {
            return;
        }

        var tag = await ShowTagImageWindowAsync(row.DisplayLabel);
        if (tag is null)
        {
            return;
        }

        await viewModel.ImageSet.LabelImageAsync(row.Image, tag);
    }

    private Task<string?> ShowTagImageWindowAsync(string currentTag)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return Task.FromResult<string?>(null);
        }

        var completion = new TaskCompletionSource<string?>();

        var input = new TextBox
        {
            Text = currentTag,
            PlaceholderText = "Image tag",
            MinWidth = 280
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 78
        };

        var saveButton = new Button
        {
            Content = "Save",
            MinWidth = 78
        };

        var dialog = new Window
        {
            Title = "Tag Image",
            Width = 360,
            Height = 156,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#161616")),
            ShowInTaskbar = false,
            Content = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(18),
                Children =
                {
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            saveButton
                        }
                    }
                }
            }
        };

        var completed = false;
        void Complete(string? value)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            completion.SetResult(value);
            dialog.Close();
        }

        cancelButton.Click += (_, _) => Complete(null);
        saveButton.Click += (_, _) => Complete(input.Text?.Trim() ?? string.Empty);
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Complete(input.Text?.Trim() ?? string.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Complete(null);
                e.Handled = true;
            }
        };
        dialog.Closed += (_, _) =>
        {
            if (!completed)
            {
                completed = true;
                completion.SetResult(null);
            }
        };

        dialog.Show(owner);
        Dispatcher.UIThread.Post(() =>
        {
            input.Focus();
            input.SelectAll();
        }, DispatcherPriority.Background);

        return completion.Task;
    }
}
