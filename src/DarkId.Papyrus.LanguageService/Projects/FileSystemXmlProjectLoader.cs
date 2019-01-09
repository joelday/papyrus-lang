using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public class FileSystemXmlProjectLoader : IXmlProjectLoader
    {
        private readonly IFileSystem _fileSystem;
        private readonly IXmlProjectDeserializer _deserializer;

        public FileSystemXmlProjectLoader(IFileSystem fileSystem, IXmlProjectDeserializer deserializer)
        {
            _fileSystem = fileSystem;
            _deserializer = deserializer;
        }

        public async Task<PapyrusProjectInfo> LoadProject(string projectPath)
        {
            using (var stream = await _fileSystem.OpenRead(projectPath))
            using (var textReader = new StreamReader(stream))
            {
                return new PapyrusProjectInfo()
                {
                    ProjectFile = projectPath,
                    Project = _deserializer.DeserializeProject(textReader.ReadToEnd())
                };
            }
        }
    }
}