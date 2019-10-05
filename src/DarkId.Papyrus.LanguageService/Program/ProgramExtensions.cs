using System;
using System.Collections.Generic;
using System.IO;
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

                    return await fileSystem.FindFiles(include.Path, options.FlagsFileName, true);
                })
                .ToArray());

            return flagsFiles.Where(t => t != null).SelectMany(t => t).LastOrDefault();
        }

        public static async Task<Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>>> ResolveSourceFileIncludes(this IFileSystem fileSystem, ProgramSources sources)
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
// TODO: Configured rule
                    var files = include.Scripts.Count > 0 ? include.Scripts.ToList() : await fileSystem.FindFiles(include.Path, "*.psc",
#if SKYRIM
                        false
#else
                        include.Recursive
#endif
                    );
                    return new Tuple<SourceInclude, IEnumerable<string>>(include, files);
                })
                .ToArray());

            var results = new Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>>();

            foreach (var include in includedFiles)
            {
                var filePaths = new Dictionary<ObjectIdentifier, string>();
                var includePath = Path.GetFullPath(include.Item1.Path);

                foreach (var fullPath in include.Item2)
                {
                    var relativePath = PathUtilities.PathNetCore.GetRelativePath(includePath, fullPath);
                    var identifier = ObjectIdentifier.FromScriptFilePath(relativePath);

                    if (filePaths.ContainsKey(identifier))
                    {
                        filePaths.Remove(identifier);
                    }

                    filePaths.Add(identifier, fullPath);
                }

                results.Add(include.Item1, filePaths);
            }

            return results;
        }

        public static Dictionary<ObjectIdentifier, string> FlattenIncludes(this Dictionary<SourceInclude, Dictionary<ObjectIdentifier, string>> includes)
        {
            var results = new Dictionary<ObjectIdentifier, string>();

            foreach (var include in includes)
            {
                foreach (var identifierFile in include.Value)
                {
                    if (results.ContainsKey(identifierFile.Key))
                    {
                        results.Remove(identifierFile.Key);
                    }

                    results.Add(identifierFile.Key, identifierFile.Value);
                }
            }

            return results;
        }
    }
}
