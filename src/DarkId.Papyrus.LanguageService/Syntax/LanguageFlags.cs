using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    [Flags]
    public enum LanguageFlags
    {
        None = 0,
        Native = 1,
        Global = 2,
        Auto = 4,
        AutoReadOnly = 8,
        DebugOnly = 16,
        Const = 32,
        BetaOnly = 64,

        All = Native | Global | Auto | AutoReadOnly | DebugOnly | Const | BetaOnly
    }
}
