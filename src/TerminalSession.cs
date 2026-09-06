using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using XTerm.Options;

namespace Goatty;

internal sealed class TerminalSession : ITabItem
{
    private readonly string _initialTitle;
    private readonly string _startingDirectory;
    private readonly string _shell;
    private readonly DispatcherTimer _titleTimer;
    private string _title;
    private string _workingDirectory = string.Empty;
    private string _processName = string.Empty;
    private string? _applicationTitle;
    private string? _manualTitle;
    private bool _closed;
    private PointerPressedEventArgs? _dragPointerPressed;
    private Point _dragStart;
    private bool _dragging;

    internal TerminalSession(TerminalGroup owner, string initialTitle)
    {
        Owner = owner;
        _initialTitle = initialTitle;
        _startingDirectory = Environment.CurrentDirectory;
        _shell = ResolveShell();
        _title = initialTitle;

        TerminalOptions options = new()
        {
            Theme = CreateNordTheme()
        };

        Control = new TerminalControl
        {
            Process = string.Empty,
            StartingDirectory = _startingDirectory,
            FontFamily = (FontFamily)Application.Current!.FindResource("TerminalFontFamily")!,
            FontSize = (double)Application.Current!.FindResource("TerminalFontSize")!,
            BufferSize = 10000,
            Background = Brush.Parse("#2E3440"),
            Foreground = Brush.Parse("#D8DEE9"),
            CursorColor = Color.Parse("#D8DEE9"),
            SelectionBrush = Brush.Parse("#665E81AC"),
            Options = options
        };

        Control.ProcessExited += OnProcessExited;
        Control.PropertyChanged += OnControlPropertyChanged;
        TerminalView.AddTitleChangedHandler(Control, OnTitleChanged);

        _titleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _titleTimer.Tick += OnTitleTimerTick;
    }

    internal event Action<TerminalSession>? Exited;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal TerminalGroup Owner { get; set; }

    internal TerminalControl Control { get; }

    public string Title => _title;

    // The status line reports where the session is and what it is running, which the title only carries in an abbreviated form.
    internal string WorkingDirectory => _workingDirectory;

    internal string ProcessName => _processName;

    public string CloseToolTip => "Close terminal (Ctrl+W)";

    internal bool HasManualTitle => _manualTitle is not null;

    internal async Task StartAsync()
    {
        await Control.LaunchProcess(_startingDirectory, _shell);
        _titleTimer.Start();
        RefreshTitle();
        Control.Focus();
    }

    internal void SetManualTitle(string title)
    {
        _manualTitle = title;
        SetTitle(title);
    }

    internal void ResetTitle()
    {
        _manualTitle = null;
        RefreshTitle();
    }

    internal void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _titleTimer.Stop();
        _titleTimer.Tick -= OnTitleTimerTick;
        Control.ProcessExited -= OnProcessExited;
        Control.PropertyChanged -= OnControlPropertyChanged;
        TerminalView.RemoveTitleChangedHandler(Control, OnTitleChanged);
        if (Control.IsLive)
        {
            Control.Kill();
        }
    }

    public void RequestClose()
    {
        Owner.CloseTerminal(this);
    }

    public ContextMenu CreateContextMenu()
    {
        MenuItem newTerminal = new() { Header = "New terminal" };
        newTerminal.Click += async (_, _) => await Owner.AddTerminalAsync();
        MenuItem newGroup = new() { Header = "New group" };
        newGroup.Click += (_, _) => Owner.RequestNewGroup();
        MenuItem renameTerminal = new() { Header = "Rename terminal" };
        renameTerminal.Click += async (_, _) => await Owner.RenameTerminalAsync(this);
        MenuItem resetTitle = new() { Header = "Reset terminal name" };
        resetTitle.Click += (_, _) => ResetTitle();
        MenuItem renameGroup = new() { Header = "Rename group" };
        renameGroup.Click += async (_, _) => await Owner.RenameAsync();
        MenuItem closeGroup = new() { Header = "Close group" };
        closeGroup.Click += (_, _) => Owner.RequestClose();

        ContextMenu menu = new() { ItemsSource = new object[] { newTerminal, newGroup, new Separator(), renameTerminal, resetTitle, new Separator(), renameGroup, closeGroup } };
        menu.Opening += (_, _) =>
        {
            Owner.SelectSession(this);
            resetTitle.IsEnabled = HasManualTitle;
        };
        return menu;
    }

    public void AttachHeaderBehavior(Control header)
    {
        header.PointerPressed += (_, e) =>
        {
            PointerPointProperties properties = e.GetCurrentPoint(header).Properties;
            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
            {
                Owner.SelectSession(this);
            }

            if (properties.IsLeftButtonPressed && e.Source is not Button)
            {
                _dragPointerPressed = e;
                _dragStart = e.GetPosition(header);
            }
        };

        header.PointerMoved += async (_, e) =>
        {
            if (_dragging || _dragPointerPressed is null || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Vector distance = e.GetPosition(header) - _dragStart;
            if (Math.Abs(distance.X) < 8 && Math.Abs(distance.Y) < 8)
            {
                return;
            }

            _dragging = true;
            DataTransfer transfer = new();
            transfer.Add(DataTransferItem.Create(MainWindow.TerminalDragFormat, this));
            await DragDrop.DoDragDropAsync(_dragPointerPressed, transfer, DragDropEffects.Move);
            _dragPointerPressed = null;
            _dragging = false;
        };

        header.PointerReleased += (_, _) =>
        {
            if (!_dragging)
            {
                _dragPointerPressed = null;
            }
        };

        header.DoubleTapped += async (_, e) =>
        {
            if (e.Source is Button)
            {
                return;
            }

            e.Handled = true;
            await Owner.RenameTerminalAsync(this);
        };

        DragDrop.SetAllowDrop(header, true);
        DragDrop.AddDragOverHandler(header, (_, e) =>
        {
            TerminalSession? dragged = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
            e.DragEffects = dragged is null || dragged == this ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        });
        DragDrop.AddDropHandler(header, (_, e) =>
        {
            TerminalSession? dragged = e.DataTransfer.TryGetValue(MainWindow.TerminalDragFormat);
            if (dragged is not null && dragged != this)
            {
                Owner.AcceptDrop(dragged, this);
                e.DragEffects = DragDropEffects.Move;
            }

            e.Handled = true;
        });
    }

    private static string ResolveShell()
    {
        string? configuredShell = Environment.GetEnvironmentVariable("SHELL")?.Trim();
        if (!string.IsNullOrEmpty(configuredShell) && File.Exists(configuredShell))
        {
            return configuredShell;
        }

        if (OperatingSystem.IsWindows())
        {
            string? commandProcessor = Environment.GetEnvironmentVariable("ComSpec")?.Trim();
            return string.IsNullOrEmpty(commandProcessor) ? "cmd.exe" : commandProcessor;
        }

        return File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
    }

    private static ThemeOptions CreateNordTheme()
    {
        return new ThemeOptions
        {
            Foreground = "#D8DEE9",
            Background = "#2E3440",
            Cursor = "#D8DEE9",
            CursorAccent = "#2E3440",
            Selection = "#5E81AC",
            Black = "#3B4252",
            Red = "#BF616A",
            Green = "#A3BE8C",
            Yellow = "#EBCB8B",
            Blue = "#81A1C1",
            Magenta = "#B48EAD",
            Cyan = "#88C0D0",
            White = "#E5E9F0",
            BrightBlack = "#4C566A",
            BrightRed = "#BF616A",
            BrightGreen = "#A3BE8C",
            BrightYellow = "#EBCB8B",
            BrightBlue = "#81A1C1",
            BrightMagenta = "#B48EAD",
            BrightCyan = "#8FBCBB",
            BrightWhite = "#ECEFF4"
        };
    }

    private void OnProcessExited(object? sender, ProcessExitedEventArgs e)
    {
        Exited?.Invoke(this);
    }

    private void OnTitleChanged(object? sender, TitleChangedEventArgs e)
    {
        _applicationTitle = string.IsNullOrWhiteSpace(e.Title) ? null : e.Title.Trim();
        RefreshTitle();
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TerminalControl.CurrentDirectoryProperty)
        {
            RefreshTitle();
        }
    }

    private void OnTitleTimerTick(object? sender, EventArgs e)
    {
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        int processId = Control.IsLive ? Control.Pid : 0;
        string? currentDirectory = LinuxProcessInfo.GetWorkingDirectory(processId) ?? Control.CurrentDirectory ?? _startingDirectory;
        string process = LinuxProcessInfo.GetForegroundProcessName(processId) ?? Path.GetFileName(_shell);
        Update(ref _workingDirectory, CollapseHome(currentDirectory), nameof(WorkingDirectory));
        Update(ref _processName, process, nameof(ProcessName));

        if (_manualTitle is not null)
        {
            SetTitle(_manualTitle);
            return;
        }

        string directoryName = GetDirectoryName(currentDirectory);
        string context = _applicationTitle ?? process;

        string title = !string.IsNullOrEmpty(directoryName) && !string.IsNullOrEmpty(context) ? $"{directoryName} : {context}" : directoryName;
        SetTitle(string.IsNullOrEmpty(title) ? _initialTitle : title);
    }

    private void SetTitle(string title)
    {
        Update(ref _title, title, nameof(Title));
    }

    private void Update(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string CollapseHome(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string cleanPath = Path.TrimEndingDirectorySeparator(path);
        string home = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (string.IsNullOrEmpty(home) || !cleanPath.StartsWith(home, StringComparison.Ordinal))
        {
            return cleanPath;
        }

        return cleanPath.Length == home.Length ? "~" : cleanPath[home.Length] == Path.DirectorySeparatorChar ? string.Concat("~", cleanPath.AsSpan(home.Length)) : cleanPath;
    }

    private static string GetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string cleanPath = Path.TrimEndingDirectorySeparator(path);
        string home = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (cleanPath == home)
        {
            return "~";
        }

        string? root = Path.GetPathRoot(cleanPath);
        if (cleanPath == root)
        {
            return cleanPath;
        }

        return Path.GetFileName(cleanPath);
    }
}
