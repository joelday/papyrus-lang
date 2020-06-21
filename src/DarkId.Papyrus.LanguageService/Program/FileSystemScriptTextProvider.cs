using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class FileSystemScriptTextProvider : IScriptTextProvider
    {
        private readonly IFileSystem _fileSystem;

        public FileSystemScriptTextProvider(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public IObservable<ScriptText> ScriptTextChanged => Observable.Empty<ScriptText>();

        public async Task<ScriptText> GetText(string filePath)
        {
            if (await _fileSystem.GetExists(filePath))
            {
                return new ScriptText(filePath, await _fileSystem.ReadAllText(filePath), await _fileSystem.GetVersion(filePath));
            }

            return null;
        }

        public async Task<string> GetTextVersion(string filePath)
        {
            return await _fileSystem.GetVersion(filePath);
        }
    }
}