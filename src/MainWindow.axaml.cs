using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Goatty;

public sealed partial class MainWindow : Window
{
    internal static readonly DataFormat<TerminalSession> TerminalDragFormat = DataFormat.CreateInProcessFormat<TerminalSession>("goatty-terminal-session");

    private readonly List<TerminalGroup> _groups = [];
    private TerminalGroup? _currentGroup;
    private int _nextGroupNumber = 1;
    private bool _initialized;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();

        GroupBar.ContextMenu = CreateGroupBarContextMenu();
        GroupBar.DoubleTapped += OnGroupBarDoubleTapped;
        DragArea.PointerPressed += OnDragAreaPointerPressed;
        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        CloseButton.Click += (_, _) => Close();
        UpdateMaximizeButton();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closing += OnClosing;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // a window state set in XAML is applied before the named controls exist
        if (change.Property == WindowStateProperty && MaximizeIcon is not null)
        {
            UpdateMaximizeButton();
        }
    }

    internal async Task AddGroupAsync()
    {
        TerminalGroup group = new(this, CreateGroupName());
        _groups.Add(group);
        GroupHeaders.Children.Add(group.Header);
        TerminalBarHost.Children.Add(group.TerminalBar);
        GroupContent.Children.Add(group);
        UpdateGroupBarVisibility();
        SelectGroup(group);
        await group.AddTerminalAsync();
    }

    internal void CloseGroup(TerminalGroup group)
    {
        if (_closing || !_groups.Contains(group))
        {
            return;
        }

        int index = _groups.IndexOf(group);
        group.CloseAllTerminals();
        _groups.Remove(group);
        GroupHeaders.Children.Remove(group.Header);
        TerminalBarHost.Children.Remove(group.TerminalBar);
        GroupContent.Children.Remove(group);

        if (_groups.Count == 0)
        {
            Close();
            return;
        }

        TerminalGroup replacement = _groups[Math.Min(index, _groups.Count - 1)];
        UpdateGroupBarVisibility();
        SelectGroup(replacement);
    }

    internal void SelectGroup(TerminalGroup group)
    {
        if (!_groups.Contains(group))
        {
            return;
        }

        _currentGroup = group;
        foreach (TerminalGroup candidate in _groups)
        {
            bool selected = candidate == group;
            candidate.IsVisible = selected;
            candidate.TerminalBar.IsVisible = selected;
            candidate.Header.SetSelected(selected);
        }

        Dispatcher.UIThread.Post(group.FocusCurrentTerminal, DispatcherPriority.Input);
    }

    internal void MoveSession(TerminalSession session, TerminalGroup destination, TerminalSession? before = null)
    {
        TerminalGroup source = session.Owner;
        if (source == destination)
        {
            destination.MoveSessionBefore(session, before);
            return;
        }

        session.Control.BeginReparent();
        source.DetachSession(session);
        destination.AttachSession(session, before);
        SelectGroup(destination);
        destination.SelectSession(session);
        destination.UpdateLayout();
        session.Control.EndReparent();

        if (source.SessionCount == 0)
        {
            CloseGroup(source);
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await AddGroupAsync();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _closing = true;
        foreach (TerminalGroup group in _groups.ToArray())
        {
            group.CloseAllTerminals();
        }

        _groups.Clear();
    }

    private async void OnGroupBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Button)
        {
            e.Handled = true;
            await AddGroupAsync();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.T)
        {
            e.Handled = true;
            _ = AddGroupAsync();
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.T)
        {
            e.Handled = true;
            if (_currentGroup is not null)
            {
                _ = _currentGroup.AddTerminalAsync();
            }
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W)
        {
            e.Handled = true;
            _currentGroup?.CloseCurrentTerminal();
            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            _currentGroup?.SelectNextTerminal();
            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            _currentGroup?.SelectPreviousTerminal();
        }
    }

    private void OnDragAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(DragArea).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateMaximizeButton()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Data = Geometry.Parse(maximized ? "M 0.5,3.5 H 7.5 V 10.5 H 0.5 Z M 2.5,3.5 V 0.5 H 10.5 V 8.5 H 7.5" : "M 0.5,0.5 H 10.5 V 10.5 H 0.5 Z");
        ToolTip.SetTip(MaximizeButton, maximized ? "Restore" : "Maximize");
    }

    private ContextMenu CreateGroupBarContextMenu()
    {
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += async (_, _) => await AddGroupAsync();
        return new ContextMenu { ItemsSource = new object[] { newGroup } };
    }

    private string CreateGroupName()
    {
        string path = Environment.CurrentDirectory;
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
        {
            name = path == Path.GetPathRoot(path) ? path : $"Group {_nextGroupNumber}";
        }

        _nextGroupNumber++;
        return name;
    }

    private void UpdateGroupBarVisibility()
    {
        GroupBar.IsVisible = _groups.Count > 1;
    }
}
