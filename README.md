# Goatty

Goatty is a minimal Qt terminal emulator based on [QTermWidget](https://github.com/lxqt/qtermwidget), the terminal widget used by [QTerminal](https://github.com/lxqt/qterminal).

The current MVP provides one window with terminal groups and dark-themed terminal tabs. The group bar stays hidden until a second group is created. Groups start with the launch directory's name and can be renamed from the terminal tab bar's context menu or by double-clicking a visible group tab. Terminal names follow their current directory and foreground process or application-provided title. Rename a terminal by double-clicking its tab or using its context menu; reset its name to resume automatic updates. Drag a terminal tab out of its tab bar and drop it on another group tab to move it. Double-click empty tab-bar space to create a terminal, and close a group's final terminal to remove the group. Closing the final group exits Goatty.

Right-click the terminal tab bar to create or manage groups. Terminal shortcuts apply to the active group:

- `Ctrl+T`: open a tab
- `Ctrl+Shift+T`: open a group
- `Ctrl+W`: close the current tab
- `Ctrl+Tab`: select the next tab
- `Ctrl+Shift+Tab`: select the previous tab

## Dependencies

- CMake 3.18 or newer
- A C++17 compiler
- Qt 6.6 or newer with the Widgets component
- QTermWidget 2.4 or newer

On Fedora:

```sh
sudo dnf install cmake ninja-build gcc-c++ qt6-qtbase-devel qtermwidget-devel
```

On Fedora Kinoite, install the same packages with `rpm-ostree` and reboot:

```sh
sudo rpm-ostree install cmake ninja-build gcc-c++ qt6-qtbase-devel qtermwidget-devel
systemctl reboot
```

## Build and run

```sh
cmake -S . -B build -G Ninja
cmake --build build
./build/goatty
```
