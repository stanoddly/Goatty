using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;

namespace Goatty;

// The chrome is a single frame around the client area. A bar marked as a rule has the frame's horizontal line drawn through it, interrupted by
// the labels sitting on it, the way Midnight Commander writes its titles into the panel border.
internal sealed class TerminalFrame : Decorator
{
    public static readonly AttachedProperty<bool> IsRuleProperty = AvaloniaProperty.RegisterAttached<TerminalFrame, Control, bool>("IsRule");

    public static readonly AttachedProperty<bool> BreaksRuleProperty = AvaloniaProperty.RegisterAttached<TerminalFrame, Control, bool>("BreaksRule");

    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<TerminalFrame, IBrush?>(nameof(LineBrush));

    // The width of background left on either side of a label, so the rule does not touch the text it runs into.
    private const double GapPadding = 6;

    private const double LineThickness = 1;

    public TerminalFrame()
    {
        LayoutUpdated += (_, _) => InvalidateVisual();
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public static void SetIsRule(Control control, bool value)
    {
        control.SetValue(IsRuleProperty, value);
    }

    public static bool GetIsRule(Control control)
    {
        return control.GetValue(IsRuleProperty);
    }

    public static void SetBreaksRule(Control control, bool value)
    {
        control.SetValue(BreaksRuleProperty, value);
    }

    public static bool GetBreaksRule(Control control)
    {
        return control.GetValue(BreaksRuleProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (LineBrush is not IBrush brush)
        {
            return;
        }

        List<double> rules = [];
        List<Rect> gaps = [];
        Collect(rules, gaps);
        if (rules.Count == 0)
        {
            return;
        }

        rules.Sort();
        gaps.Sort((left, right) => left.X.CompareTo(right.X));

        foreach (double y in rules)
        {
            DrawRule(context, brush, y, gaps);
        }

        double top = rules[0];
        double bottom = rules[^1];
        if (bottom > top)
        {
            context.FillRectangle(brush, new Rect(0, top, LineThickness, bottom - top));
            context.FillRectangle(brush, new Rect(Bounds.Width - LineThickness, top, LineThickness, bottom - top));
        }
    }

    private void Collect(List<double> rules, List<Rect> gaps)
    {
        Stack<Visual> pending = new();
        pending.Push(this);

        while (pending.Count > 0)
        {
            foreach (Visual child in pending.Pop().GetVisualChildren())
            {
                // The terminal renders its own content and holds no chrome, so its subtree is never worth walking.
                if (child is TerminalControl || child is not Control control || !control.IsEffectivelyVisible)
                {
                    continue;
                }

                if (control.TransformToVisual(this) is Matrix transform)
                {
                    Rect bounds = new Rect(control.Bounds.Size).TransformToAABB(transform);
                    if (GetIsRule(control))
                    {
                        rules.Add(Math.Round(bounds.Center.Y - (LineThickness / 2)));
                    }

                    if (GetBreaksRule(control))
                    {
                        gaps.Add(bounds.Inflate(new Thickness(GapPadding, 0)));
                    }
                }

                pending.Push(control);
            }
        }
    }

    // A gap belongs to whichever rules pass through it, so a label overlaying a bar it does not live in still breaks that bar's line.
    private void DrawRule(DrawingContext context, IBrush brush, double y, List<Rect> gaps)
    {
        double x = 0;
        foreach (Rect gap in gaps)
        {
            if (y < gap.Top || y > gap.Bottom || gap.Right <= x)
            {
                continue;
            }

            // Gaps can overlap or nest, so a segment is only drawn where one starts beyond the line already covered.
            if (gap.X > x)
            {
                context.FillRectangle(brush, new Rect(x, y, Math.Min(gap.X, Bounds.Width) - x, LineThickness));
            }

            x = Math.Max(x, gap.Right);
        }

        if (x < Bounds.Width)
        {
            context.FillRectangle(brush, new Rect(x, y, Bounds.Width - x, LineThickness));
        }
    }
}
