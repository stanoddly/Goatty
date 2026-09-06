# Goatty

Goatty is a minimal terminal emulator built with [Avalonia](https://avaloniaui.net/) and [Iciclecreek.Avalonia.Terminal](https://github.com/tomlm/Iciclecreek.Avalonia.Terminal).

Goatty provides one window with terminal groups and terminal tabs. The whole window is drawn as terminal output: the window has no system decorations, and the bars, tabs, menus and prompts share the terminal's monospace font and Nord palette instead of standing on chrome of their own. Tabs are written as `[ name ]`, with the selected one bold and bright. `[-]`, `[□]` and `[x]` at the top right minimize, maximize and close the window; dragging a bar moves the window and the window edges resize it.

The group bar stays hidden until a second group is created. Groups start with the launch directory's name and can be renamed from a context menu or by double-clicking a visible group tab. Renaming happens inline on the bar, as `(rename-session)` or `(rename-window)`: `Enter` accepts the name and `Escape` keeps the old one.

Terminal names follow their current directory and foreground process or application-provided title. Rename a terminal by double-clicking its tab or using its context menu; reset its name to resume automatic updates. Drag a terminal tab onto another group tab to move it, or onto another terminal tab to position it before that terminal. Double-click empty terminal-bar space to create a terminal, and close a group's final terminal to remove the group. Closing the final group exits Goatty.

Terminal shortcuts apply to the active group:

- `Ctrl+T`: open a terminal
- `Ctrl+Shift+T`: open a group
- `Ctrl+W`: close the current terminal
- `Ctrl+Tab`: select the next terminal
- `Ctrl+Shift+Tab`: select the previous terminal

## Dependencies

- .NET 10 SDK
- Avalonia 12.1.1
- Iciclecreek.Avalonia.Terminal 3.1.0

NuGet restores the Avalonia, XTerm.NET, and Porta.Pty dependencies, including the native PTY library used on Linux and macOS.

On Linux, Goatty uses Avalonia's native Wayland backend when a compositor is available and falls back to X11 otherwise.

On Fedora, install the .NET 10 SDK if it is not already available:

```sh
sudo dnf install dotnet-sdk-10.0
```

## Build and run

```sh
dotnet build Goatty.slnx
dotnet run --project Goatty.csproj
```

To create a self-contained Linux build:

```sh
dotnet publish Goatty.csproj -c Release -r linux-x64 --self-contained
```
