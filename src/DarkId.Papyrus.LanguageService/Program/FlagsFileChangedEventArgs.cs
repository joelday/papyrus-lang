using System;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class FlagsFileChangedEventArgs : EventArgs
    {
        public FlagsFileChangedEventArgs(FlagsFile flagsFile)
        {
            FlagsFile = flagsFile;
        }

        public FlagsFile FlagsFile { get; private set; }
    }
}