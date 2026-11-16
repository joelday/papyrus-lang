using System;
using System.IO;

namespace Papyrus.Workspaces;

/// <summary>
/// The user's <see href="https://github.com/fireundubh/pyro">Pyro</see>. Projects are resolved
/// with Pyro's library against this install, so remote imports come from its download cache and
/// are fetched with its access token.
/// </summary>
public sealed class PyroInstall
{
    /// <summary>Set by the editor to the user's configured Pyro executable.</summary>
    public const string EnvironmentVariable = "PAPYRUS_PYRO";

    public const string ReleasesUrl = "https://github.com/fireundubh/pyro/releases/latest";

    public PyroInstall(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ExecutablePath = Path.GetFullPath(executablePath);
        Directory = Path.GetDirectoryName(ExecutablePath)!;
    }

    public string ExecutablePath { get; }

    /// <summary>Pyro's program directory, which holds its <c>remote</c> cache and <c>.secrets</c>.</summary>
    public string Directory { get; }

    /// <summary>
    /// The configured executable if it exists, else <see cref="EnvironmentVariable"/>, else the
    /// first Pyro on the PATH. Null when there is none.
    /// </summary>
    public static PyroInstall? Locate(string? configuredPath = null)
    {
        foreach (var candidate in new[] { configuredPath, Environment.GetEnvironmentVariable(EnvironmentVariable) })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return new PyroInstall(candidate);
            }
        }

        var names = OperatingSystem.IsWindows() ? new[] { "pyro.exe", "pyro.cmd", "pyro.bat" } : new[] { "pyro" };
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim('"'), name);
                    if (File.Exists(candidate))
                    {
                        return new PyroInstall(candidate);
                    }
                }
                catch (ArgumentException)
                {
                }
            }
        }

        return null;
    }
}
