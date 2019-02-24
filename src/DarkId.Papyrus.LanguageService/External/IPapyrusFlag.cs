using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.External
{
    interface IPapyrusFlag
    {
        uint Value { get; }

        bool AllowedOnObj { get; }

        bool AllowedOnProp { get; }

        bool AllowedOnVar { get; }

        bool AllowedOnStructVar { get; }

        bool AllowedOnFunc { get; }

        bool AllowedOnGroup { get; }
    }
}