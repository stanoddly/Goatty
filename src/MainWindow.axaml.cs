using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Goatty;

public sealed partial class MainWindow : Window
{
    internal static readonly DataFormat<TerminalSession> TerminalDragFormat = DataFormat.CreateInProcessFormat<TerminalSession>("goatty-terminal-session");

    private readonly ObservableCollection<TerminalGroup> _groups = [];
    private int _nextGroupNumber = 1;
    private bool _initialized;
    private bool _closing;
    private bool _updatingSelection;

    public MainWindow()
    {
        InitializeComponent();

        GroupBar.Padding = new Thickness(8, 3, WindowChrome.ControlsWidth, 3);
        WindowControlsHost.Children.Add(WindowChrome.CreateControls(this));
        WindowChrome.AttachResizeBorder(this);
        WindowChrome.AttachMoveHandle(this, GroupBar);

        GroupTabs.ItemsSource = _groups;
        GroupTabs.ItemTemplate = TabHeader.Template;
        GroupTabs.SelectionChanged += OnGroupSelectionChanged;
        GroupBar.ContextMenu = CreateGroupBarContextMenu();
        GroupBar.DoubleTapped += OnGroupBarDoubleTapped;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closing += OnClosing;
    }

    internal async Task AddGroupAsync()
    {
        TerminalGroup group = new(this, CreateGroupName());
        _groups.Add(group);
        GroupContent.Children.Add(group.View);
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
        GroupContent.Children.Remove(group.View);

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

        GroupTabs.SelectedItem = group;
        UpdateGroupVisibility();
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
        destination.View.UpdateLayout();
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
            if (CurrentGroup is TerminalGroup group)
            {
                _ = group.AddTerminalAsync();
            }
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W)
        {
            e.Handled = true;
            CurrentGroup?.CloseCurrentTerminal();
            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            CurrentGroup?.SelectNextTerminal();
            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            CurrentGroup?.SelectPreviousTerminal();
        }
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

    // A group is renamed on the group bar when it is showing, and otherwise on its own terminal bar, so the prompt always lands on a visible line.
    internal Panel? VisibleGroupBarContent => GroupBar.IsVisible ? GroupBarContent : null;

    private void UpdateGroupBarVisibility()
    {
        GroupBar.IsVisible = _groups.Count > 1;
    }

    private TerminalGroup? CurrentGroup => GroupTabs.SelectedItem as TerminalGroup;

    private void OnGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        UpdateGroupVisibility();
        if (CurrentGroup is TerminalGroup group)
        {
            Dispatcher.UIThread.Post(group.FocusCurrentTerminal, DispatcherPriority.Input);
        }
    }

    private void UpdateGroupVisibility()
    {
        _updatingSelection = true;
        try
        {
            if (CurrentGroup is null && _groups.Count > 0)
            {
                GroupTabs.SelectedItem = _groups[0];
            }

            foreach (TerminalGroup candidate in _groups)
            {
                candidate.View.IsVisible = candidate == CurrentGroup;
            }
        }
        finally
        {
            _updatingSelection = false;
        }
    }
}
