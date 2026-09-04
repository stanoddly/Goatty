using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

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
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ITabItem.Title))
            {
                title.Text = item.Title;
            }
        };

        // The multiplication sign sits on the font's math axis rather than the centre of the button, so the cross is drawn instead.
        Path closeGlyph = new()
        {
            Data = Geometry.Parse("M 0,0 L 8,8 M 8,0 L 0,8"),
            StrokeThickness = 1,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Button closeButton = new() { Content = closeGlyph };
        ToolTip.SetTip(closeButton, item.CloseToolTip);
        closeButton.Classes.Add("tab-close");
        closeButton.Click += (_, _) => item.RequestClose();
        DockPanel.SetDock(closeButton, Dock.Right);

        DockPanel header = new();
        header.Children.Add(closeButton);
        header.Children.Add(title);
        header.ContextMenu = item.CreateContextMenu();
        item.AttachHeaderBehavior(header);
        return header;
    });
}
