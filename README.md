# Goatty

Goatty is a minimal terminal emulator built with [Avalonia](https://avaloniaui.net/) and [Iciclecreek.Avalonia.Terminal](https://github.com/tomlm/Iciclecreek.Avalonia.Terminal).

Goatty provides one window with terminal groups and dark-themed terminal tabs. The group bar stays hidden until a second group is created. Groups start with the launch directory's name and can be renamed from a context menu or by double-clicking a visible group tab.

Terminal names follow their current directory and foreground process or application-provided title. Rename a terminal by double-clicking its tab or using its context menu; reset its name to resume automatic updates. Drag a terminal tab onto another group tab to move it, or onto another terminal tab to position it before that terminal. Double-click empty terminal-bar space to create a terminal, and close a group's final terminal to remove the group. Closing the final group exits Goatty.

Terminal shortcuts apply to the active group:

- `Ctrl+T`: open a terminal
- `Ctrl+Shift+T`: open a group
- `Ctrl+W`: close the current terminal
- `Ctrl+Tab`: select the next terminal
- `Ctrl+Shift+Tab`: select the previous terminal

## Dependencies

- .NET 10 SDK
- Avalonia 12.0.2
- Iciclecreek.Avalonia.Terminal 3.1.0

NuGet restores the Avalonia, XTerm.NET, and Porta.Pty dependencies, including the native PTY library used on Linux and macOS.

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
