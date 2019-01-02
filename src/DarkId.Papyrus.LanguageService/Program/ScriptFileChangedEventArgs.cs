using System;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class ScriptFileChangedEventArgs : EventArgs
    {
        public ScriptFileChangedEventArgs(ScriptFile scriptFile)
        {
            ScriptFile = scriptFile;
        }

        public ScriptFile ScriptFile { get; private set; }
    }
}