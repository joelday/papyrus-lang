using System;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class ScriptTextChangedEventArgs : EventArgs
    {
        public ScriptTextChangedEventArgs(ScriptText scriptText)
        {
            ScriptText = scriptText;
        }

        public ScriptText ScriptText { get; private set; }
    }
}