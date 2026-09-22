using System.IO;
using System.Runtime.InteropServices;

namespace KaneO.HashMe.App;

public static class ShortcutResolver
{
    public static string Resolve(string droppedPath)
    {
        var fullPath = Path.GetFullPath(droppedPath);
        var extension = Path.GetExtension(fullPath);

        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveShellLink(fullPath) ?? fullPath;
        }

        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveInternetShortcut(fullPath) ?? fullPath;
        }

        return fullPath;
    }

    private static string? ResolveShellLink(string path)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [path]);
            if (shortcut is null)
            {
                return null;
            }

            var target = shortcut.GetType().InvokeMember(
                "TargetPath",
                System.Reflection.BindingFlags.GetProperty,
                binder: null,
                target: shortcut,
                args: null) as string;
            return string.IsNullOrWhiteSpace(target) ? null : Path.GetFullPath(target);
        }
        catch (COMException)
        {
            return null;
        }
        catch (System.Reflection.TargetInvocationException)
        {
            return null;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string? ResolveInternetShortcut(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var raw = line[4..].Trim();
                if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    return uri.LocalPath;
                }
            }
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }
}
