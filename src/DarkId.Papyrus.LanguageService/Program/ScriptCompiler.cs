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
using System.Threading;

namespace DarkId.Papyrus.LanguageService.Program
{
    class CompilerObjectLoadResults
    {
        public ScriptObjectType Type { get; set; }
        public IReadOnlyDictionary<IToken, ScriptComplexType> TokenTypes { get; private set; }
        public IReadOnlyDictionary<IToken, HashSet<ScriptScope>> TokenScopes { get; private set; }

        public CompilerObjectLoadResults(ScriptObjectType type, IReadOnlyDictionary<IToken, ScriptComplexType> tokenTypes, IReadOnlyDictionary<IToken, HashSet<ScriptScope>> tokenScopes)
        {
            Type = type;
            TokenTypes = tokenTypes;
            TokenScopes = tokenScopes;
        }
    }

    class ScriptCompiler : PCompiler.Compiler
    {
        #region Patches

        [HarmonyPatch(typeof(PCompiler.Compiler))]
        [HarmonyPatch("LoadObject", typeof(string), typeof(Dictionary<string, ScriptComplexType>), typeof(Stack<string>), typeof(bool), typeof(ScriptObjectType))]
        public static class LoadObjectPatch
        {
            public static bool Prefix(
                ScriptCompiler __instance, ref ScriptObjectType __result,
                string asObjectName, Dictionary<string, ScriptComplexType> apKnownTypes, Stack<string> apChildren, bool abErrorOnNoObject, ScriptObjectType apImmediateChild)
            {
                var objectIdentifier = ObjectIdentifier.Parse(asObjectName);

                if (objectIdentifier == __instance._targetScript.Id || apKnownTypes.ContainsKey(asObjectName))
                {
                    return true;
                }

                if (__instance._targetScript.Program.ScriptFiles.ContainsKey(objectIdentifier))
                {
                    var externalType = __instance._targetScript.Program.ScriptFiles[objectIdentifier].CompilerType;
                    if (externalType != null)
                    {
                        __result = externalType;
                        return false;
                    }
                }

                return true;
            }
        }

        //private static void AttachParserInternalErrorEventHandler(PapyrusParser parser, ScriptCompiler compiler)
        //{
        //    var eventInfo = parser.GetType().GetEvent("pErrorHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        //    var eventHandlerType = eventInfo.EventHandlerType;

        //    var handlerMethodInfo = typeof(PCompiler.Compiler).GetMethod("OnInternalError", BindingFlags.Instance | BindingFlags.NonPublic);
        //    var handlerInstance = Delegate.CreateDelegate(eventHandlerType, compiler, handlerMethodInfo);

        //    eventInfo.AddMethod.Invoke(parser, new object[] { handlerInstance });
        //}

        [HarmonyPatch(typeof(PCompiler.Compiler), "Parse")]
        public static class ParsePatch
        {
            public static bool Prefix(
                ScriptCompiler __instance,
                ITokenStream apTokenStream, out ScriptObjectType apParsedObj, out IToken arpParentName, out Dictionary<string, IToken> apImports)
            {
                __instance._logger.LogTrace("Parsing {0}...", __instance._targetScript.Id);

                var parser = new PapyrusParser(apTokenStream);

                // TODO: Currently suppressing parser errors so that the compiler will continue with type resolution and disambiguation.
                // AttachParserInternalErrorEventHandler(parser, __instance);

                var parserDynamic = parser.AsDynamic();
                parserDynamic.KnownUserFlags = __instance.AsDynamic().pFlagDict;

                parser.script();

                apParsedObj = parser.ParsedObject;
                arpParentName = parser.ParentObjName;
                apImports = parser.pImportedItems;

                return false;
            }
        }

        #endregion

        private static readonly MethodInfo _disambiguateTypeMethod =
            typeof(PCompiler.Compiler).GetMethod("DisambiguateType", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly ScriptFile _targetScript;
        private readonly ILogger _logger;

        private readonly dynamic _thisDynamic;

        public ScriptCompiler(ScriptFile targetScript, ILogger logger) : base()
        {
            _thisDynamic = this.AsDynamic();

            _targetScript = targetScript;
            _logger = logger;

            _thisDynamic.pObjectToPath = new Dictionary<string, string>();
            SetPathForObject(_targetScript.Id, _targetScript.FilePath);

            _thisDynamic.pFlagDict = _targetScript.Program.FlagsFile.NativeFlagsDict;
        }

        public ScriptObjectType Load(Dictionary<string, ScriptComplexType> knownTypes)
        {
            lock (knownTypes)
            {
                if (knownTypes.ContainsKey(_targetScript.Id))
                {
                    knownTypes.Remove(_targetScript.Id);
                }
            }

            ScriptObjectType result = null;

            try
            {
                result = (ScriptObjectType)_thisDynamic.LoadObject(_targetScript.Id.ToString(), knownTypes);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error in LoadObject for {_targetScript.Id}");
            }

            if (result == null)
            {
                return null;
            }

            if (result.pImportedTypes == null)
            {
                result.pImportedTypes = new Dictionary<string, ScriptObjectType>();
            }

            if (result.pImportedNamespaces == null)
            {
                result.pImportedNamespaces = new HashSet<string>();
            }

            return result;
        }

        public string DisambiguateType(string asRawType, IToken apErrorToken, ScriptObjectType apContainingType, Dictionary<string, ScriptComplexType> apKnownTypes)
        {
            return (string)_disambiguateTypeMethod.Invoke(this, new object[] { asRawType, apErrorToken, apContainingType, apKnownTypes });
        }
    }
}