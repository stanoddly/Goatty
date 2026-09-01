using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Goatty;

internal sealed class TextPromptWindow : Window
{
    private readonly TextBox _textBox;

    private TextPromptWindow(string title, string label, string initialValue)
    {
        Title = title;
        Width = 420;
        Height = 150;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _textBox = new TextBox { Text = initialValue };
        Button cancelButton = new() { Content = "Cancel", MinWidth = 80 };
        cancelButton.Click += (_, _) => Close(null);
        Button acceptButton = new() { Content = "OK", MinWidth = 80 };
        acceptButton.Click += (_, _) => Accept();

        StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(acceptButton);

        Grid content = new() { Margin = new Avalonia.Thickness(16), RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        content.Children.Add(new TextBlock { Text = label, Margin = new Avalonia.Thickness(0, 0, 0, 6) });
        Grid.SetRow(_textBox, 1);
        content.Children.Add(_textBox);
        Grid.SetRow(buttons, 2);
        buttons.Margin = new Avalonia.Thickness(0, 12, 0, 0);
        content.Children.Add(buttons);
        Content = content;

        Opened += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
        KeyDown += OnKeyDown;
    }

    internal static Task<string?> ShowAsync(Window owner, string title, string label, string initialValue)
    {
        TextPromptWindow prompt = new(title, label, initialValue);
        return prompt.ShowDialog<string?>(owner);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Accept();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private void Accept()
    {
        string value = _textBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(value))
        {
            Close(value);
        }
    }
}
