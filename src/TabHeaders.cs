using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Goatty;

// Implemented by anything that can appear in one of the tab strips, so both strips share a single header template.
internal interface ITabItem : INotifyPropertyChanged
{
    string Title { get; }

    string CloseToolTip { get; }

    void RequestClose();

    ContextMenu CreateContextMenu();

    // Drag and drop differs between the two strips, so each item wires its own gestures onto the generated header.
    void AttachHeaderBehavior(Control header);
}

internal static class TabHeader
{
    internal static readonly FuncDataTemplate<ITabItem> Template = new((item, _) =>
    {
        if (item is null)
        {
            return new TextBlock();
        }

        TextBlock title = new()
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 240,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        title.Classes.Add("tab-text");
        title.Classes.Add("tab-title");
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ITabItem.Title))
            {
                title.Text = item.Title;
            }
        };

        Button closeButton = new() { Content = "x", VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(closeButton, item.CloseToolTip);
        closeButton.Classes.Add("terminal-text");
        closeButton.Classes.Add("tab-close");
        closeButton.Click += (_, _) => item.RequestClose();

        // The rule is interrupted around the whole header, so the margin has to outrun that gap for a stub of line to show between two tabs.
        StackPanel header = new() { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 0, 20, 0) };
        TerminalFrame.SetBreaksRule(header, true);
        header.Children.Add(title);
        header.Children.Add(closeButton);
        header.ContextMenu = item.CreateContextMenu();
        item.AttachHeaderBehavior(header);
        return header;
    });
}
