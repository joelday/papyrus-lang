using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public static class ProgramExtensions
    {
        internal static Diagnostic ToDiagnostic(this Exception exception, Range range = default(Range))
        {
            return new Diagnostic(DiagnosticLevel.Error, $"Exception: {exception.Message}", range, exception);
        }

        public static async Task<string> ResolveFlagsFile(this IFileSystem fileSystem, ProgramOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FlagsFileName))
            {
                return null;
            }

            var flagsFiles = await Task.WhenAll(options.Sources.Includes
                .AsParallel()
                .Select(async (include) =>
                {
                    if (!await fileSystem.GetExists(include.Path))
                    {
                        return null;
                    }

                    return await fileSystem.FindFiles(include.Path, options.FlagsFileName, include.Recursive);
                })
                .ToArray());

            return flagsFiles.Where(t => t != null).SelectMany(t => t).LastOrDefault();
        }

        public static async Task<Dictionary<ObjectIdentifier, string>> ResolveSourceFiles(this IFileSystem fileSystem, ProgramSources sources)
        {
            var includedFiles = await Task.WhenAll(sources.Includes
                .AsParallel()
                .AsOrdered()
                .Select(async (include) =>
                {
                    if (!await fileSystem.GetExists(include.Path))
                    {
                        return new Tuple<SourceInclude, IEnumerable<string>>(include, new string[] { });
                    }

                    var files = await fileSystem.FindFiles(include.Path, "*.psc", include.Recursive);
                    return new Tuple<SourceInclude, IEnumerable<string>>(include, files);
                })
                .ToArray());

            var results = new Dictionary<ObjectIdentifier, string>();
            var filePaths = new Dictionary<string, ObjectIdentifier>(StringComparer.OrdinalIgnoreCase);

            foreach (var include in includedFiles)
            {
                foreach (var fullPath in include.Item2)
                {
                    if (filePaths.ContainsKey(fullPath))
                    {
                        results.Remove(filePaths[fullPath]);
                        filePaths.Remove(fullPath);
                    }

                    var relativePath = fullPath.Substring(include.Item1.Path.Length + 1);
                    var identifier = ObjectIdentifier.FromScriptFilePath(relativePath);

                    filePaths.Add(fullPath, identifier);

                    if (results.ContainsKey(identifier))
                    {
                        results.Remove(identifier);
                    }

                    results.Add(identifier, fullPath);
                }
            }

            return results;
        }
    }
}