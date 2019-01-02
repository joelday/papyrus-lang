using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public interface IXmlProjectLocator
    {
        Task<IEnumerable<string>> FindProjectFiles(string rootPath);
    }
}