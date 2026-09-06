using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Goatty;

// Renaming happens on the bar itself, the way tmux edits a name on its status line, so no native dialog breaks the illusion.
internal static class InlinePrompt
{
    internal static Task<string?> ShowAsync(Panel host, string label, string initialValue)
    {
        TaskCompletionSource<string?> completion = new();
        Control[] hidden = [.. host.Children.Where(child => child.IsVisible)];
        foreach (Control child in hidden)
        {
            child.IsVisible = false;
        }

        TextBlock prompt = new() { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        prompt.Classes.Add("terminal-dim");
        DockPanel.SetDock(prompt, Dock.Left);

        TextBox input = new() { Text = initialValue, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        input.Classes.Add("terminal-input");

        // The prompt takes over a bar the frame draws a rule through, so it has to break that rule the way the tabs it replaces do.
        DockPanel line = new();
        TerminalFrame.SetBreaksRule(line, true);
        line.Children.Add(prompt);
        line.Children.Add(input);
        host.Children.Add(line);

        void Finish(string? result)
        {
            if (!completion.TrySetResult(result))
            {
                return;
            }

            host.Children.Remove(line);
            foreach (Control child in hidden)
            {
                child.IsVisible = true;
            }
        }

        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Finish(input.Text);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Finish(null);
            }
        };
        // A context menu item can hand focus back after the prompt appears, so losing focus only cancels once the prompt has really held it.
        bool focused = false;
        input.GotFocus += (_, _) => focused = true;
        input.LostFocus += (_, _) =>
        {
            if (focused)
            {
                Finish(null);
            }
        };

        Dispatcher.UIThread.Post(() =>
        {
            input.Focus();
            input.SelectAll();
        }, DispatcherPriority.Input);

        return completion.Task;
    }
}
