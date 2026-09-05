using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Goatty;

// The window has no system decorations, so the bars have to provide the move handle, the resize edges and the window buttons themselves.
internal static class WindowChrome
{
    internal const double ControlsWidth = 96;

    private const double ResizeMargin = 5;
    private const double MoveThreshold = 4;

    private static readonly Dictionary<WindowEdge, Cursor> ResizeCursors = new()
    {
        [WindowEdge.North] = new Cursor(StandardCursorType.TopSide),
        [WindowEdge.South] = new Cursor(StandardCursorType.BottomSide),
        [WindowEdge.West] = new Cursor(StandardCursorType.LeftSide),
        [WindowEdge.East] = new Cursor(StandardCursorType.RightSide),
        [WindowEdge.NorthWest] = new Cursor(StandardCursorType.TopLeftCorner),
        [WindowEdge.NorthEast] = new Cursor(StandardCursorType.TopRightCorner),
        [WindowEdge.SouthWest] = new Cursor(StandardCursorType.BottomLeftCorner),
        [WindowEdge.SouthEast] = new Cursor(StandardCursorType.BottomRightCorner)
    };

    internal static void AttachResizeBorder(Window window)
    {
        // The terminal fills the window and consumes pointer input, so the edges are claimed on the way down.
        window.AddHandler(InputElement.PointerMovedEvent, (_, e) => UpdateCursor(window, e), RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) => BeginResize(window, e), RoutingStrategies.Tunnel);
    }

    internal static void AttachMoveHandle(Window window, Control handle)
    {
        // BeginMoveDrag needs the press that started the gesture, so the bar keeps it until the pointer has actually travelled.
        PointerPressedEventArgs? press = null;
        Point origin = default;

        handle.PointerPressed += (_, e) =>
        {
            bool draggable = e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed && IsBarSurface(handle, e.Source);
            press = draggable ? e : null;
            origin = e.GetPosition(handle);
        };
        handle.PointerReleased += (_, _) => press = null;
        handle.PointerMoved += (_, e) =>
        {
            if (press is null || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Moving on press would swallow the bar's own click and double click gestures, so the drag waits for real movement.
            Point position = e.GetPosition(handle);
            if (Math.Abs(position.X - origin.X) < MoveThreshold && Math.Abs(position.Y - origin.Y) < MoveThreshold)
            {
                return;
            }

            PointerPressedEventArgs started = press;
            press = null;
            window.BeginMoveDrag(started);
        };
    }

    internal static Control CreateControls(Window window)
    {
        StackPanel controls = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Width = ControlsWidth
        };

        controls.Children.Add(CreateControl("[-]", "Minimize", () => window.WindowState = WindowState.Minimized));
        controls.Children.Add(CreateControl("[□]", "Maximize", () => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));

        Button close = CreateControl("[x]", "Close", window.Close);
        close.Classes.Add("window-close");
        controls.Children.Add(close);
        return controls;
    }

    private static Button CreateControl(string glyph, string toolTip, Action action)
    {
        Button control = new() { Content = glyph };
        ToolTip.SetTip(control, toolTip);
        control.Classes.Add("terminal-text");
        control.Classes.Add("window-control");
        control.Click += (_, _) => action();
        return control;
    }

    private static bool IsBarSurface(Control handle, object? source)
    {
        return source is Control control && control.FindAncestorOfType<Button>() is null && control.FindAncestorOfType<TabStripItem>() is null && (control == handle || handle.IsVisualAncestorOf(control));
    }

    private static void UpdateCursor(Window window, PointerEventArgs e)
    {
        WindowEdge? edge = GetEdge(window, e.GetPosition(window));
        Cursor cursor = edge is null ? Cursor.Default : ResizeCursors[edge.Value];
        if (window.Cursor != cursor)
        {
            window.Cursor = cursor;
        }
    }

    private static void BeginResize(Window window, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed || GetEdge(window, e.GetPosition(window)) is not WindowEdge edge)
        {
            return;
        }

        e.Handled = true;
        window.BeginResizeDrag(edge, e);
    }

    private static WindowEdge? GetEdge(Window window, Point position)
    {
        if (window.WindowState != WindowState.Normal || !window.CanResize)
        {
            return null;
        }

        bool left = position.X <= ResizeMargin;
        bool right = position.X >= window.Bounds.Width - ResizeMargin;
        bool top = position.Y <= ResizeMargin;
        bool bottom = position.Y >= window.Bounds.Height - ResizeMargin;

        if (top)
        {
            return left ? WindowEdge.NorthWest : right ? WindowEdge.NorthEast : WindowEdge.North;
        }

        if (bottom)
        {
            return left ? WindowEdge.SouthWest : right ? WindowEdge.SouthEast : WindowEdge.South;
        }

        return left ? WindowEdge.West : right ? WindowEdge.East : null;
    }
}
