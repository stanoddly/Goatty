namespace Goatty;

internal static class LinuxProcessInfo
{
    internal static string? GetWorkingDirectory(int processId)
    {
        if (!OperatingSystem.IsLinux() || processId <= 0)
        {
            return null;
        }

        try
        {
            DirectoryInfo link = new($"/proc/{processId}/cwd");
            return link.ResolveLinkTarget(true)?.FullName;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? GetForegroundProcessName(int shellProcessId)
    {
        if (!OperatingSystem.IsLinux() || shellProcessId <= 0)
        {
            return null;
        }

        string? stat = ReadText($"/proc/{shellProcessId}/stat");
        int foregroundProcessGroup = ParseForegroundProcessGroup(stat);
        int processId = foregroundProcessGroup > 0 ? foregroundProcessGroup : shellProcessId;
        return ReadText($"/proc/{processId}/comm")?.Trim();
    }

    private static int ParseForegroundProcessGroup(string? stat)
    {
        if (string.IsNullOrEmpty(stat))
        {
            return 0;
        }

        int commandEnd = stat.LastIndexOf(')');
        if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
        {
            return 0;
        }

        // Fields following the command start with state; tpgid is the sixth field in this suffix.
        string[] fields = stat[(commandEnd + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 5 && int.TryParse(fields[5], out int processGroup) ? processGroup : 0;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
