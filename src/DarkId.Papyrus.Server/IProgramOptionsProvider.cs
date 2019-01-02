using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.Server
{
    public interface IProgramOptionsProvider
    {
        Task<Dictionary<string, ProgramOptions>> GetProgramOptions();
    }
}
