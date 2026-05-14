using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
// ReSharper disable ConvertToStaticClass
// ReSharper disable ClassNeverInstantiated.Global

namespace Difflection.Infrastructure;

public sealed class InlineRenameTextBoxBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<InlineRenameTextBoxBehavior, TextBox, bool>("IsEnabled");

    public static readonly AttachedProperty<ICommand?> CommitCommandProperty =
        AvaloniaProperty.RegisterAttached<InlineRenameTextBoxBehavior, TextBox, ICommand?>("CommitCommand");

    public static readonly AttachedProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.RegisterAttached<InlineRenameTextBoxBehavior, TextBox, ICommand?>("CancelCommand");

    static InlineRenameTextBoxBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextBox>(OnIsEnabledChanged);
    }

    private InlineRenameTextBoxBehavior()
    {
    }

    public static bool GetIsEnabled(TextBox textBox)
    {
        return textBox.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(TextBox textBox, bool value)
    {
        textBox.SetValue(IsEnabledProperty, value);
    }

    public static ICommand? GetCommitCommand(TextBox textBox)
    {
        return textBox.GetValue(CommitCommandProperty);
    }

    public static void SetCommitCommand(TextBox textBox, ICommand? value)
    {
        textBox.SetValue(CommitCommandProperty, value);
    }

    public static ICommand? GetCancelCommand(TextBox textBox)
    {
        return textBox.GetValue(CancelCommandProperty);
    }

    public static void SetCancelCommand(TextBox textBox, ICommand? value)
    {
        textBox.SetValue(CancelCommandProperty, value);
    }

    private static void OnIsEnabledChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            textBox.AttachedToVisualTree += TextBox_OnAttachedToVisualTree;
            textBox.PropertyChanged += TextBox_OnPropertyChanged;
            textBox.AddHandler(InputElement.LostFocusEvent, TextBox_OnLostFocus);
            textBox.KeyDown += TextBox_OnKeyDown;
            FocusInlineNameEditor(textBox);
        }
        else
        {
            textBox.AttachedToVisualTree -= TextBox_OnAttachedToVisualTree;
            textBox.PropertyChanged -= TextBox_OnPropertyChanged;
            textBox.RemoveHandler(InputElement.LostFocusEvent, TextBox_OnLostFocus);
            textBox.KeyDown -= TextBox_OnKeyDown;
        }
    }

    private static void TextBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        FocusInlineNameEditor(sender as TextBox);
    }

    private static void TextBox_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            FocusInlineNameEditor(sender as TextBox);
        }
    }

    private static void TextBox_OnLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Execute(GetCommitCommand(textBox), textBox.DataContext);
        }
    }

    private static void TextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Execute(GetCommitCommand(textBox), textBox.DataContext);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Execute(GetCancelCommand(textBox), textBox.DataContext);
            e.Handled = true;
        }
    }

    private static void Execute(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private static void FocusInlineNameEditor(TextBox? textBox)
    {
        if (textBox is not { IsVisible: true })
        {
            return;
        }

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
