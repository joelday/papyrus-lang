using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Common
{
    public class LocalFileSystem : IFileSystem
    {
        public Task<IEnumerable<string>> FindFiles(string rootPath, string pattern, bool recursive)
        {
            return Task.FromResult(Directory
                .EnumerateFiles(rootPath, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .ToArray()
                .AsEnumerable());
        }

        public Task<bool> GetExists(string path)
        {
            return Task.FromResult(Directory.Exists(path) || File.Exists(path));
        }

        public Task<string> GetVersion(string path)
        {
            return Task.FromResult(File.GetLastWriteTimeUtc(path).Ticks.ToString());
        }

        public Task<Stream> OpenRead(string path)
        {
            return Task.FromResult((Stream)File.OpenRead(path));
        }
    }
}