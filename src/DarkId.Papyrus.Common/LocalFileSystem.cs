using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class LocalFileSystem : IFileSystem
    {
        public IAsyncEnumerable<string> FindFiles(string rootPath, string pattern, bool recursive)
        {
            return Directory
                .EnumerateFiles(rootPath, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .ToArray()
                .ToAsyncEnumerable();
        }

        public async Task<bool> GetExists(string path)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        public async Task<string> GetVersion(string path)
        {
            return File.GetLastWriteTimeUtc(path).Ticks.ToString();
        }

        public async Task<Stream> OpenRead(string path)
        {
            return (Stream)File.OpenRead(path);
        }
    }
}