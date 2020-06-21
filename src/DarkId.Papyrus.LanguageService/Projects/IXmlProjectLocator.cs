using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public interface IXmlProjectLocator
    {
        IAsyncEnumerable<string> FindProjectFiles(string rootPath);
    }
}