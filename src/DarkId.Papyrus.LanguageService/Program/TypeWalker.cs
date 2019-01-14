using PCompiler;
using ReflectionMagic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using DarkId.Papyrus.LanguageService.Program;
using Harmony;
using Antlr.Runtime;
using DarkId.Papyrus.Common;
using System.Runtime.Serialization;
using Antlr.Runtime.Tree;
using Microsoft.Extensions.Logging;
using Antlr.Runtime.Misc;
using DarkId.Papyrus.LanguageService.Compiler;

namespace DarkId.Papyrus.LanguageService.Program
{
    class TypeWalker : PapyrusTypeWalker
    {
        public class Adaptor : CommonTreeAdaptor
        {

        }


        public class Stream : CommonTreeNodeStream
        {
            public Stream(ScriptObjectType type) : base(type.GetAst())
            {
                TokenStream = type.GetTokenStream();
            }
        }

        private readonly ScriptFile _targetScript;
        private readonly Stream _input;
        private readonly ILogger _logger;

        public TypeWalker(ScriptFile targetScript, ScriptObjectType type, ILogger logger) : base(new Stream(type))
        {
            _targetScript = targetScript;
            _input = (Stream)input;
            _logger = logger;

            TreeAdaptor = new Adaptor();
        }
    }
}