using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using XTerm.Options;

namespace Goatty;

internal sealed class TerminalSession
{
    private readonly string _initialTitle;
    private readonly string _startingDirectory;
    private readonly string _shell;
    private readonly DispatcherTimer _titleTimer;
    private string? _applicationTitle;
    private string? _manualTitle;
    private bool _closed;

    internal TerminalSession(TerminalGroup owner, string initialTitle)
    {
        Owner = owner;
        _initialTitle = initialTitle;
        _startingDirectory = Environment.CurrentDirectory;
        _shell = ResolveShell();
        Title = initialTitle;

        TerminalOptions options = new()
        {
            Theme = CreateNordTheme()
        };

        Control = new TerminalControl
        {
            Process = string.Empty,
            StartingDirectory = _startingDirectory,
            FontSize = 13,
            BufferSize = 10000,
            Background = Brush.Parse("#2E3440"),
            Foreground = Brush.Parse("#D8DEE9"),
            CursorColor = Color.Parse("#D8DEE9"),
            SelectionBrush = Brush.Parse("#665E81AC"),
            Options = options
        };

        Header = new TerminalTabHeader(this);
        Control.ProcessExited += OnProcessExited;
        Control.PropertyChanged += OnControlPropertyChanged;
        TerminalView.AddTitleChangedHandler(Control, OnTitleChanged);

        _titleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _titleTimer.Tick += OnTitleTimerTick;
    }

    internal event Action<TerminalSession>? Exited;

    internal TerminalGroup Owner { get; set; }

    internal TerminalControl Control { get; }

    internal TerminalTabHeader Header { get; }

    internal string Title { get; private set; }

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
        if (_manualTitle is not null)
        {
            SetTitle(_manualTitle);
            return;
        }

        int processId = Control.IsLive ? Control.Pid : 0;
        string? currentDirectory = LinuxProcessInfo.GetWorkingDirectory(processId) ?? Control.CurrentDirectory ?? _startingDirectory;
        string directoryName = GetDirectoryName(currentDirectory);
        string context = _applicationTitle ?? LinuxProcessInfo.GetForegroundProcessName(processId) ?? Path.GetFileName(_shell);

        string title = !string.IsNullOrEmpty(directoryName) && !string.IsNullOrEmpty(context) ? $"{directoryName} : {context}" : directoryName;
        SetTitle(string.IsNullOrEmpty(title) ? _initialTitle : title);
    }

    private void SetTitle(string title)
    {
        if (Title == title)
        {
            return;
        }

        Title = title;
        Header.UpdateTitle(title);
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
