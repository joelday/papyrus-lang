using System;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.Test.LanguageService.Program.TestHarness;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    public abstract class ProgramTestBase : IDisposable
    {
        private readonly PapyrusProgram _program;
        protected PapyrusProgram Program => _program;

        protected ProgramTestBase()
        {
            _program = ProgramTestHarness.CreateProgram();
            _program.ResolveSources().Wait();
        }

        public void Dispose()
        {
            _program.Dispose();
        }
    }
}
