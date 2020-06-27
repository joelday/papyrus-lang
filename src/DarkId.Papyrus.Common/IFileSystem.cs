using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public interface IFileSystem
    {
        Task<bool> GetExists(string path);
        IAsyncEnumerable<string> FindFiles(string rootPath, string pattern, bool recursive);
        Task<Stream> OpenRead(string path);
        Task<string> GetVersion(string path);
    }
}