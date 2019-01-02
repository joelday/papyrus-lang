using System;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public interface IScriptTextProvider
    {
        Task<ScriptText> GetText(string filePath);
        Task<string> GetTextVersion(string filePath);
        event EventHandler<ScriptTextChangedEventArgs> OnScriptTextChanged;
    }
}