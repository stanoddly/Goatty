using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Goatty;

internal sealed class TerminalGroup : ITabItem
{
    private readonly MainWindow _window;
    private readonly TabStrip _tabs;
    private readonly Grid _content;
    private readonly ObservableCollection<TerminalSession> _sessions = [];
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

        Button addButton = new() { Content = "+" };
        ToolTip.SetTip(addButton, "New terminal (Ctrl+T)");
        addButton.Classes.Add("new-terminal");
        addButton.Click += async (_, _) => await AddTerminalAsync();

        DockPanel tabBarContent = new();
        DockPanel.SetDock(addButton, Dock.Right);
        tabBarContent.Children.Add(addButton);
        tabBarContent.Children.Add(tabScroller);

        Border tabBar = new() { Child = tabBarContent, BorderThickness = new Thickness(0, 0, 0, 1) };
        tabBar[!Border.BackgroundProperty] = tabBar.GetResourceObservable("TabBarBrush").ToBinding();
        tabBar[!Border.BorderBrushProperty] = tabBar.GetResourceObservable("TabBarBorderBrush").ToBinding();
        tabBar.DoubleTapped += async (_, e) =>
        {
            if (e.Source is not Button)
            {
                e.Handled = true;
                await AddTerminalAsync();
            }
        };
        tabBar.ContextMenu = CreateTabBarContextMenu();

        _content = new Grid();
        Grid.SetRow(_content, 1);

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.Children.Add(tabBar);
        root.Children.Add(_content);
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
        string? title = await TextPromptWindow.ShowAsync(_window, "Rename group", "Group name:", Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }

    internal async Task RenameTerminalAsync(TerminalSession session)
    {
        string? title = await TextPromptWindow.ShowAsync(_window, "Rename terminal", "Terminal name:", session.Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            session.SetManualTitle(title.Trim());
        }
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
