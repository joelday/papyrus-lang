using System;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public interface IScriptTextProvider
    {
        Task<ScriptText> GetText(string filePath);
        Task<string> GetTextVersion(string filePath);
        IObservable<ScriptText> ScriptTextChanged { get; }
    }
}