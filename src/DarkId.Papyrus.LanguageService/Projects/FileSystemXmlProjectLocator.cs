using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using DarkId.Papyrus.Common;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public class FileSystemXmlProjectLocator : IXmlProjectLocator
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public FileSystemXmlProjectLocator(IFileSystem fileSystem, ILogger<FileSystemXmlProjectLocator> logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async Task<IEnumerable<string>> FindProjectFiles(string rootPath)
        {
            _logger.LogInformation($"Searching for Papyrus projects in {rootPath}...");
            var projectFiles = await _fileSystem.FindFiles(rootPath, "*.ppj", true).ToArrayAsync();
            _logger.LogInformation($"Found {projectFiles.Count()} project(s) in {rootPath}:{System.Environment.NewLine}{string.Join(System.Environment.NewLine, projectFiles)}");

            return projectFiles;
        }
    }
}