using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Common
{
    public static class FileSystemExtensions
    {
        public static async Task<string> ReadAllText(this IFileSystem fileSystem, string path)
        {
            using (var stream = await fileSystem.OpenRead(path))
            using (var streamReader = new StreamReader(stream))
            {
                return await streamReader.ReadToEndAsync();
            }
        }
    }
}