using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public interface IXmlProjectLoader
    {
        Task<PapyrusProjectInfo> LoadProject(string projectPath);
    }
}