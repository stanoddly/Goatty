using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Goatty;

internal sealed class TerminalGroup : ITabItem
{
    private readonly MainWindow _window;
    private readonly TabStrip _tabs;
    private readonly Grid _content;
    private readonly Panel _barContent;
    private readonly TextBlock _statusDirectory;
    private readonly TextBlock _statusProcess;
    private readonly ObservableCollection<TerminalSession> _sessions = [];
    private TerminalSession? _statusSource;
    private string _title;
    private int _nextTerminalNumber = 1;
    private bool _closing;
    private bool _updatingSelection;

    internal TerminalGroup(MainWindow window, string title)
    {
        _window = window;
        _title = title;

        _tabs = new TabStrip { ItemsSource = _sessions, ItemTemplate = TabHeader.Template };
        _tabs.SelectionChanged += OnSelectionChanged;

        ScrollViewer tabScroller = new()
        {
            Content = _tabs,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        tabScroller.Classes.Add("tab-scroller");

        Button addButton = new() { Content = "+", VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(addButton, "New terminal (Ctrl+T)");
        addButton.Classes.Add("terminal-text");
        TerminalFrame.SetBreaksRule(addButton, true);
        addButton.Click += async (_, _) => await AddTerminalAsync();

        // Two auto columns keep the plus sign directly after the last tab, and hand it the space the scrolling tabs give up.
        Grid tabLine = new() { ColumnDefinitions = new ColumnDefinitions("Auto,Auto") };
        Grid.SetColumn(addButton, 1);
        tabLine.Children.Add(tabScroller);
        tabLine.Children.Add(addButton);

        _barContent = new Panel();
        _barContent.Children.Add(tabLine);

        // A brushless border is skipped by hit testing, so the bar needs a background to be pressed as the window's move handle.
        Border tabBar = new() { Child = _barContent, Padding = new Thickness(14, 3, WindowChrome.ControlsWidth, 3), Background = Brushes.Transparent };
        TerminalFrame.SetIsRule(tabBar, true);
        tabBar.DoubleTapped += async (_, e) =>
        {
            if (e.Source is not Button)
            {
                e.Handled = true;
                await AddTerminalAsync();
            }
        };
        tabBar.ContextMenu = CreateTabBarContextMenu();
        WindowChrome.AttachMoveHandle(window, tabBar);

        _content = new Grid { Margin = new Thickness(3, 0) };
        Grid.SetRow(_content, 1);

        _statusDirectory = CreateStatusText("status-path");
        // The path is the DockPanel's filling child, so it has to hug its text or the gap it breaks would swallow the whole line.
        _statusDirectory.HorizontalAlignment = HorizontalAlignment.Left;
        TerminalFrame.SetBreaksRule(_statusDirectory, true);
        _statusProcess = CreateStatusText("status-process");
        TextBlock hints = CreateStatusText("status-hint");
        hints.Text = "^T new  ^W close  ^Tab switch";

        StackPanel statusRight = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        statusRight.Children.Add(_statusProcess);
        statusRight.Children.Add(hints);
        TerminalFrame.SetBreaksRule(statusRight, true);
        DockPanel.SetDock(statusRight, Dock.Right);

        DockPanel statusLine = new();
        statusLine.Children.Add(statusRight);
        statusLine.Children.Add(_statusDirectory);

        Border statusBar = new() { Child = statusLine, Padding = new Thickness(14, 3) };
        TerminalFrame.SetIsRule(statusBar, true);
        Grid.SetRow(statusBar, 2);

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Children.Add(tabBar);
        root.Children.Add(_content);
        root.Children.Add(statusBar);
        View = root;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Control View { get; }

    internal int SessionCount => _sessions.Count;

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public string CloseToolTip => "Close group";

    public void RequestClose()
    {
        _window.CloseGroup(this);
    }

    public ContextMenu CreateContextMenu()
    {
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += (_, _) => RequestNewGroup();
        MenuItem renameGroup = new() { Header = "Rename group" };
        renameGroup.Click += async (_, _) => await RenameAsync();
        MenuItem closeGroup = new() { Header = "Close group" };
        closeGroup.Click += (_, _) => RequestClose();

        ContextMenu menu = new() { ItemsSource = new object[] { newGroup, new Separator(), renameGroup, closeGroup } };
        menu.Opening += (_, _) => _window.SelectGroup(this);
        return menu;
    }

    public void AttachHeaderBehavior(Control header)
    {
        header.DoubleTapped += async (_, e) =>
        {
            if (e.Source is Button)
            {
                return;
            }

            e.Handled = true;
            await RenameAsync();
        };

        DragDrop.SetAllowDrop(header, true);
        DragDrop.AddDragEnterHandler(header, (_, e) =>
        {
            if (e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat) is null)
            {
                return;
            }

            _window.SelectGroup(this);
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        });
        DragDrop.AddDragOverHandler(header, (_, e) =>
        {
            e.DragEffects = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat) is null ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        });
        DragDrop.AddDropHandler(header, (_, e) =>
        {
            TerminalSession? dragged = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
            if (dragged is not null)
            {
                AcceptDrop(dragged, null);
                e.DragEffects = DragDropEffects.Move;
            }

            e.Handled = true;
        });
    }

    internal async Task AddTerminalAsync()
    {
        TerminalSession session = new(this, $"Shell {_nextTerminalNumber++}");
        AttachSession(session, null);
        SelectSession(session);
        await session.StartAsync();
    }

    internal void AttachSession(TerminalSession session, TerminalSession? before)
    {
        session.Owner = this;
        int index = before is null ? _sessions.Count : Math.Max(0, _sessions.IndexOf(before));
        _sessions.Insert(index, session);
        _content.Children.Add(session.Control);
        session.Exited += OnSessionExited;
    }

    internal void DetachSession(TerminalSession session)
    {
        if (!_sessions.Remove(session))
        {
            return;
        }

        session.Exited -= OnSessionExited;
        _content.Children.Remove(session.Control);
        UpdateSelection();
    }

    internal void MoveSessionBefore(TerminalSession session, TerminalSession? before)
    {
        int currentIndex = _sessions.IndexOf(session);
        if (currentIndex < 0)
        {
            return;
        }

        int destinationIndex = before is null ? _sessions.Count : _sessions.IndexOf(before);
        if (destinationIndex < 0 || currentIndex == destinationIndex)
        {
            return;
        }

        if (currentIndex < destinationIndex)
        {
            destinationIndex--;
        }

        _sessions.Move(currentIndex, destinationIndex);
        SelectSession(session);
    }

    internal void SelectSession(TerminalSession session)
    {
        if (!_sessions.Contains(session))
        {
            return;
        }

        _tabs.SelectedItem = session;
        UpdateSelection();
        Dispatcher.UIThread.Post(() => session.Control.Focus(), DispatcherPriority.Input);
    }

    internal void FocusCurrentTerminal()
    {
        CurrentSession?.Control.Focus();
    }

    internal void CloseCurrentTerminal()
    {
        if (CurrentSession is TerminalSession session)
        {
            CloseTerminal(session);
        }
    }

    internal void CloseTerminal(TerminalSession session)
    {
        int index = _sessions.IndexOf(session);
        if (index < 0)
        {
            return;
        }

        session.Exited -= OnSessionExited;
        _sessions.RemoveAt(index);
        _content.Children.Remove(session.Control);
        session.Close();

        if (_sessions.Count == 0)
        {
            if (!_closing)
            {
                _window.CloseGroup(this);
            }
            return;
        }

        SelectSession(_sessions[Math.Min(index, _sessions.Count - 1)]);
    }

    internal void CloseAllTerminals()
    {
        _closing = true;
        if (_statusSource is not null)
        {
            _statusSource.PropertyChanged -= OnStatusSourceChanged;
            _statusSource = null;
        }

        foreach (TerminalSession session in _sessions.ToArray())
        {
            session.Exited -= OnSessionExited;
            session.Close();
        }

        _sessions.Clear();
        _content.Children.Clear();
    }

    internal void SelectNextTerminal()
    {
        SelectRelativeTerminal(1);
    }

    internal void SelectPreviousTerminal()
    {
        SelectRelativeTerminal(-1);
    }

    internal async Task RenameAsync()
    {
        string? title = await InlinePrompt.ShowAsync(_window.VisibleGroupBarContent ?? _barContent, "(rename-session)", Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        FocusCurrentTerminal();
    }

    internal async Task RenameTerminalAsync(TerminalSession session)
    {
        string? title = await InlinePrompt.ShowAsync(_barContent, "(rename-window)", session.Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            session.SetManualTitle(title.Trim());
        }

        FocusCurrentTerminal();
    }

    internal void RequestNewGroup()
    {
        _ = _window.AddGroupAsync();
    }

    internal void AcceptDrop(TerminalSession session, TerminalSession? before)
    {
        _window.MoveSession(session, this, before);
    }

    private TerminalSession? CurrentSession => _tabs.SelectedItem as TerminalSession;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        UpdateSelection();
        if (CurrentSession is TerminalSession session)
        {
            Dispatcher.UIThread.Post(() => session.Control.Focus(), DispatcherPriority.Input);
        }
    }

    private void OnSessionExited(TerminalSession session)
    {
        Dispatcher.UIThread.Post(() => CloseTerminal(session));
    }

    private void SelectRelativeTerminal(int offset)
    {
        if (_sessions.Count < 2 || CurrentSession is not TerminalSession current)
        {
            return;
        }

        int currentIndex = _sessions.IndexOf(current);
        int index = (currentIndex + offset + _sessions.Count) % _sessions.Count;
        SelectSession(_sessions[index]);
    }

    private void UpdateSelection()
    {
        _updatingSelection = true;
        try
        {
            if (CurrentSession is null && _sessions.Count > 0)
            {
                _tabs.SelectedItem = _sessions[0];
            }

            foreach (TerminalSession session in _sessions)
            {
                session.Control.IsVisible = session == CurrentSession;
            }
        }
        finally
        {
            _updatingSelection = false;
        }

        UpdateStatusSource();
    }

    // The status line follows whichever session is on screen, so it swaps its subscription along with the selection.
    private void UpdateStatusSource()
    {
        TerminalSession? session = CurrentSession;
        if (_statusSource == session)
        {
            UpdateStatus();
            return;
        }

        if (_statusSource is not null)
        {
            _statusSource.PropertyChanged -= OnStatusSourceChanged;
        }

        _statusSource = session;
        if (_statusSource is not null)
        {
            _statusSource.PropertyChanged += OnStatusSourceChanged;
        }

        UpdateStatus();
    }

    private void OnStatusSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        SetStatusText(_statusDirectory, _statusSource?.WorkingDirectory);
        SetStatusText(_statusProcess, _statusSource?.ProcessName);
    }

    // An empty run would leave a hole in the rule where no text is, so a status field with nothing to say is taken off the line entirely.
    private static void SetStatusText(TextBlock field, string? text)
    {
        field.Text = text ?? string.Empty;
        field.IsVisible = !string.IsNullOrEmpty(text);
    }

    private static TextBlock CreateStatusText(string styleClass)
    {
        TextBlock field = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        field.Classes.Add(styleClass);
        return field;
    }

    private ContextMenu CreateTabBarContextMenu()
    {
        MenuItem newTerminal = new() { Header = "New terminal" };
        newTerminal.Click += async (_, _) => await AddTerminalAsync();
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += (_, _) => RequestNewGroup();
        MenuItem renameGroup = new() { Header = "Rename group" };
        renameGroup.Click += async (_, _) => await RenameAsync();
        MenuItem closeGroup = new() { Header = "Close group" };
        closeGroup.Click += (_, _) => RequestClose();

        return new ContextMenu { ItemsSource = new object[] { newTerminal, newGroup, new Separator(), renameGroup, closeGroup } };
    }
}
