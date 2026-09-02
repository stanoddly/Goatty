using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Goatty;

internal sealed class TerminalGroup : Grid
{
    private readonly MainWindow _window;
    private readonly StackPanel _headers;
    private readonly Grid _content;
    private readonly List<TerminalSession> _sessions = [];
    private TerminalSession? _currentSession;
    private int _nextTerminalNumber = 1;
    private bool _closing;

    internal TerminalGroup(MainWindow window, string title)
    {
        _window = window;
        Header = new GroupTabHeader(this, title);
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));

        _headers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        ScrollViewer headerScroller = new()
        {
            Content = _headers,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        Button addButton = new() { Content = "+" };
        ToolTip.SetTip(addButton, "New terminal (Ctrl+T)");
        addButton.Classes.Add("new-terminal");
        addButton.Click += async (_, _) => await AddTerminalAsync();

        DockPanel headerPanel = new();
        DockPanel.SetDock(addButton, Dock.Right);
        headerPanel.Children.Add(addButton);
        headerPanel.Children.Add(headerScroller);

        Border terminalBar = new() { Background = Brush.Parse("#3B4252"), Padding = new Thickness(4, 2), Child = headerPanel };
        terminalBar.DoubleTapped += async (_, e) =>
        {
            if (e.Source is not Button)
            {
                e.Handled = true;
                await AddTerminalAsync();
            }
        };
        terminalBar.ContextMenu = CreateTerminalBarContextMenu();

        _content = new Grid();
        SetRow(_content, 1);
        Children.Add(terminalBar);
        Children.Add(_content);
    }

    internal GroupTabHeader Header { get; }

    internal int SessionCount => _sessions.Count;

    internal void HeaderSelected()
    {
        _window.SelectGroup(this);
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
        _headers.Children.Insert(index, session.Header);
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
        _headers.Children.Remove(session.Header);
        _content.Children.Remove(session.Control);

        if (_currentSession == session)
        {
            _currentSession = _sessions.FirstOrDefault();
            UpdateSelection();
        }
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

        _sessions.RemoveAt(currentIndex);
        _headers.Children.RemoveAt(currentIndex);
        if (currentIndex < destinationIndex)
        {
            destinationIndex--;
        }

        _sessions.Insert(destinationIndex, session);
        _headers.Children.Insert(destinationIndex, session.Header);
        SelectSession(session);
    }

    internal void SelectSession(TerminalSession session)
    {
        if (!_sessions.Contains(session))
        {
            return;
        }

        _currentSession = session;
        UpdateSelection();
        Dispatcher.UIThread.Post(() => session.Control.Focus(), DispatcherPriority.Input);
    }

    internal void FocusCurrentTerminal()
    {
        _currentSession?.Control.Focus();
    }

    internal void CloseCurrentTerminal()
    {
        if (_currentSession is not null)
        {
            CloseTerminal(_currentSession);
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
        _headers.Children.Remove(session.Header);
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

        _currentSession = _sessions[Math.Min(index, _sessions.Count - 1)];
        UpdateSelection();
        FocusCurrentTerminal();
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
        _headers.Children.Clear();
        _content.Children.Clear();
        _currentSession = null;
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
        string? title = await TextPromptWindow.ShowAsync(_window, "Rename group", "Group name:", Header.Title);
        if (!string.IsNullOrWhiteSpace(title))
        {
            Header.Title = title.Trim();
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

    internal void RequestClose()
    {
        _window.CloseGroup(this);
    }

    internal void AcceptDrop(TerminalSession session, TerminalSession? before)
    {
        _window.MoveSession(session, this, before);
    }

    private void OnSessionExited(TerminalSession session)
    {
        Dispatcher.UIThread.Post(() => CloseTerminal(session));
    }

    private void SelectRelativeTerminal(int offset)
    {
        if (_sessions.Count < 2 || _currentSession is null)
        {
            return;
        }

        int currentIndex = _sessions.IndexOf(_currentSession);
        int index = (currentIndex + offset + _sessions.Count) % _sessions.Count;
        SelectSession(_sessions[index]);
    }

    private void UpdateSelection()
    {
        foreach (TerminalSession session in _sessions)
        {
            bool selected = session == _currentSession;
            session.Control.IsVisible = selected;
            session.Header.SetSelected(selected);
        }
    }

    private ContextMenu CreateTerminalBarContextMenu()
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
