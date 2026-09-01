using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Goatty;

internal sealed class GroupTabHeader : Border
{
    private static readonly IBrush SelectedBackground = Brush.Parse("#4C566A");
    private static readonly IBrush UnselectedBackground = Brush.Parse("#3B4252");

    private readonly TerminalGroup _group;
    private readonly TextBlock _title;

    internal GroupTabHeader(TerminalGroup group, string title)
    {
        _group = group;
        _title = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };

        Button closeButton = new() { Content = "×" };
        ToolTip.SetTip(closeButton, "Close group");
        closeButton.Classes.Add("tab-close");
        closeButton.Click += (_, _) => _group.RequestClose();
        DockPanel.SetDock(closeButton, Dock.Right);

        DockPanel content = new();
        content.Children.Add(closeButton);
        content.Children.Add(_title);
        Child = content;
        Padding = new Thickness(9, 2, 2, 2);
        CornerRadius = new CornerRadius(4);
        Background = UnselectedBackground;
        ContextMenu = CreateContextMenu();

        PointerPressed += OnPointerPressed;
        DoubleTapped += OnDoubleTapped;
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragEnterHandler(this, OnDragEnter);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    internal string Title
    {
        get => _title.Text ?? string.Empty;
        set => _title.Text = value;
    }

    internal void SetSelected(bool selected)
    {
        Background = selected ? SelectedBackground : UnselectedBackground;
    }

    private ContextMenu CreateContextMenu()
    {
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += (_, _) => _group.RequestNewGroup();
        MenuItem renameGroup = new() { Header = "Rename group" };
        renameGroup.Click += async (_, _) => await _group.RenameAsync();
        MenuItem closeGroup = new() { Header = "Close group" };
        closeGroup.Click += (_, _) => _group.RequestClose();
        return new ContextMenu { ItemsSource = new object[] { newGroup, new Separator(), renameGroup, closeGroup } };
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
        {
            _group.HeaderSelected();
        }
    }

    private async void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Button)
        {
            return;
        }

        e.Handled = true;
        await _group.RenameAsync();
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        TerminalSession? session = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
        if (session is null)
        {
            return;
        }

        _group.HeaderSelected();
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        TerminalSession? session = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
        e.DragEffects = session is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        TerminalSession? session = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
        if (session is not null)
        {
            _group.AcceptDrop(session, null);
            e.DragEffects = DragDropEffects.Move;
        }

        e.Handled = true;
    }
}

internal sealed class TerminalTabHeader : Border
{
    private static readonly IBrush SelectedBackground = Brush.Parse("#4C566A");
    private static readonly IBrush UnselectedBackground = Brush.Parse("#3B4252");

    private readonly TerminalSession _session;
    private readonly TextBlock _title;
    private PointerPressedEventArgs? _dragPointerPressed;
    private Point _dragStart;
    private bool _dragging;

    internal TerminalTabHeader(TerminalSession session)
    {
        _session = session;
        _title = new TextBlock { Text = session.Title, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 240, TextTrimming = TextTrimming.CharacterEllipsis };

        Button closeButton = new() { Content = "×" };
        ToolTip.SetTip(closeButton, "Close terminal (Ctrl+W)");
        closeButton.Classes.Add("tab-close");
        closeButton.Click += (_, _) => _session.Owner.CloseTerminal(_session);
        DockPanel.SetDock(closeButton, Dock.Right);

        DockPanel content = new();
        content.Children.Add(closeButton);
        content.Children.Add(_title);
        Child = content;
        Padding = new Thickness(9, 2, 2, 2);
        CornerRadius = new CornerRadius(4);
        Background = UnselectedBackground;
        ContextMenu = CreateContextMenu();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        DoubleTapped += OnDoubleTapped;
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    internal void UpdateTitle(string title)
    {
        _title.Text = title;
    }

    internal void SetSelected(bool selected)
    {
        Background = selected ? SelectedBackground : UnselectedBackground;
    }

    private ContextMenu CreateContextMenu()
    {
        MenuItem newTerminal = new() { Header = "New terminal" };
        newTerminal.Click += async (_, _) => await _session.Owner.AddTerminalAsync();
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += (_, _) => _session.Owner.RequestNewGroup();
        MenuItem renameTerminal = new() { Header = "Rename terminal" };
        renameTerminal.Click += async (_, _) => await _session.Owner.RenameTerminalAsync(_session);
        MenuItem resetTitle = new() { Header = "Reset terminal name" };
        resetTitle.Click += (_, _) => _session.ResetTitle();
        MenuItem renameGroup = new() { Header = "Rename group" };
        renameGroup.Click += async (_, _) => await _session.Owner.RenameAsync();
        MenuItem closeGroup = new() { Header = "Close group" };
        closeGroup.Click += (_, _) => _session.Owner.RequestClose();

        ContextMenu menu = new() { ItemsSource = new object[] { newTerminal, newGroup, new Separator(), renameTerminal, resetTitle, new Separator(), renameGroup, closeGroup } };
        menu.Opening += (_, _) =>
        {
            _session.Owner.SelectSession(_session);
            resetTitle.IsEnabled = _session.HasManualTitle;
        };
        return menu;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
        {
            _session.Owner.SelectSession(_session);
        }

        if (properties.IsLeftButtonPressed && e.Source is not Button)
        {
            _dragPointerPressed = e;
            _dragStart = e.GetPosition(this);
        }
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging || _dragPointerPressed is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Vector distance = e.GetPosition(this) - _dragStart;
        if (Math.Abs(distance.X) < 8 && Math.Abs(distance.Y) < 8)
        {
            return;
        }

        _dragging = true;
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(MainWindow.TerminalDragFormat, _session));
        await DragDrop.DoDragDropAsync(_dragPointerPressed, transfer, DragDropEffects.Move);
        _dragPointerPressed = null;
        _dragging = false;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            _dragPointerPressed = null;
        }
    }

    private async void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Button)
        {
            return;
        }

        e.Handled = true;
        await _session.Owner.RenameTerminalAsync(_session);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        TerminalSession? session = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
        e.DragEffects = session is null || session == _session ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        TerminalSession? session = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
        if (session is not null && session != _session)
        {
            _session.Owner.AcceptDrop(session, _session);
            e.DragEffects = DragDropEffects.Move;
        }

        e.Handled = true;
    }
}
