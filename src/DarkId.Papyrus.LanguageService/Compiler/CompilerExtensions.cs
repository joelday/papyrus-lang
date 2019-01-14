using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Compiler;
using Antlr.Runtime;
using System.Text;
using PCompiler;
using DarkId.Papyrus.LanguageService.Program;
using System;
using Antlr.Runtime.Tree;
using System.Collections.Generic;
using ReflectionMagic;
using System.Reflection;

namespace DarkId.Papyrus.LanguageService.Compiler
{
    static class CompilerExtensions
    {
        public static CommonTree GetAst(this ScriptObjectType objectType)
        {
#if FALLOUT4
            return objectType.pObjAST;
#elif SKYRIM
            return objectType.kAST;
#endif
        }

        public static ITokenStream GetTokenStream(this ScriptObjectType objectType)
        {
#if FALLOUT4
            return objectType.pObjTokenStream;
#elif SKYRIM
            return objectType.kTokenStream;
#endif
        }

        public static Dictionary<string, IPapyrusFlag> GetFlags(this FlagsParser flagsParser)
        {
            var mappedDict = new Dictionary<string, IPapyrusFlag>();

            dynamic flagsDict = flagsParser.AsDynamic().DefinedFlags;

            foreach (var pair in flagsDict)
            {
                dynamic asDynamicPair = ReflectionMagic.PrivateReflectionUsingDynamicExtensions.AsDynamic(pair);
                mappedDict.Add(asDynamicPair.Key, new PapyrusFlag(asDynamicPair.Value));
            }

            return mappedDict;
        }

        public static void OnError(this PapyrusParser parser, Action<object, ErrorEventArgs> handler)
        {
            OnError(parser, "pErrorHandler", handler);
        }

        public static void OnError(this PapyrusLexer lexer, Action<object, ErrorEventArgs> handler)
        {
            OnError(lexer, "pErrorHandler", handler);
        }

        public static void OnError(this PapyrusTypeWalker typeWalker, Action<object, ErrorEventArgs> handler)
        {
            OnError(typeWalker, "pErrorHandler", handler);
        }

        public static void OnError(this FlagsLexer lexer, Action<object, ErrorEventArgs> handler)
        {
            OnError(lexer, "ErrorHandler", handler);
        }

        public static void OnError(this FlagsParser parser, Action<object, ErrorEventArgs> handler)
        {
            OnError(parser, "ErrorHandler", handler);
        }

        public static void OnError(object errorEventEmitter, string errorEventName, Action<object, ErrorEventArgs> handler)
        {
            var eventInfo = errorEventEmitter.GetType().GetEvent(errorEventName, BindingFlags.Instance | BindingFlags.NonPublic);
            var eventHandlerType = eventInfo.EventHandlerType;

            Action<object, object> innerHandler = (object sender, object args) => handler(sender, new ErrorEventArgs(args));
            var del = Delegate.CreateDelegate(eventHandlerType, innerHandler.Target, innerHandler.Method);

            eventInfo.AddMethod.Invoke(errorEventEmitter, new object[] { del });
        }

        public static AstType GetAstType(this ITree treeNode)
        {
            return (AstType)treeNode.Type;
        }

        public static Diagnostic ToDiagnostic(this ErrorEventArgs e)
        {
            return new Diagnostic(DiagnosticLevel.Error, e.ErrorText, new Range()
            {
                Start = new Position()
                {
                    Line = Math.Max(0, e.LineNumber - 1),
                    Character = e.ColumnNumber
                },
                End = new Position()
                {
                    Line = Math.Max(0, e.LineNumber - 1),
                    Character = e.ColumnNumber
                }
            });
        }

        public static Diagnostic ToDiagnostic(this CompilerErrorEventArgs e)
        {
            return new Diagnostic(DiagnosticLevel.Error, e.Message, new Range());
        }
    }
}