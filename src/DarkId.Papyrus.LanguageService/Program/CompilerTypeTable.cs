using PCompiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Program
{
    class CompilerTypeTable
    {
        private readonly Dictionary<string, ScriptComplexType> _types =
            new Dictionary<string, ScriptComplexType>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ScriptComplexType> Types => _types;
    }
}
