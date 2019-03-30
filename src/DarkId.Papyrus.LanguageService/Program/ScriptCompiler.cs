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
using DarkId.Papyrus.LanguageService.External;

#if SKYRIM
using ScriptComplexType = PCompiler.ScriptObjectType;
#endif

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

#if SKYRIM
        [HarmonyPatch(typeof(PCompiler.Compiler))]
        [HarmonyPatch("TypeCheck", typeof(ScriptObjectType), typeof(Dictionary<string, ScriptObjectType>), typeof(Stack<string>))]
        public static class TypeCheckPatch
        {
            // Skyrim's LoadObject method calls TypeCheck internally, which mutates the AST and prevents a subsequent type check from succeeding.
            // So, this patch is just to replace it with a no-op.
            public static bool Prefix(ScriptCompiler __instance, ScriptObjectType akObj, Dictionary<string, ScriptObjectType> akKnownTypes, Stack<string> akChildren)
            {
                return false;
            }
        }
#endif

#if FALLOUT4

        // This prevents parsing from failing while a function identifier is incomplete.
        [HarmonyPatch(typeof(PCompiler.ScriptFunctionType))]
        [HarmonyPatch(MethodType.Constructor, typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool))]
        public static class ScriptFunctionTypeCtorPatch
        {
            public static bool Prefix(ScriptFunctionType __instance, ref string asFuncName, ref string asObjName, ref string asStateName, ref string asPropName, ref bool abIsEvent)
            {
                if (string.IsNullOrEmpty(asFuncName))
                {
                    asFuncName = "__Unknown__";
                }

                return true;
            }
        }
#endif

        [HarmonyPatch(typeof(PCompiler.Compiler))]
        [HarmonyPatch("LoadObject", typeof(string), typeof(Dictionary<string, ScriptComplexType>), typeof(Stack<string>), typeof(bool), typeof(ScriptObjectType))]
        public static class LoadObjectPatch
        {
#if FALLOUT4
            public static bool Prefix(
                ScriptCompiler __instance, ref ScriptObjectType __result,
                string asObjectName, Dictionary<string, ScriptComplexType> apKnownTypes, Stack<string> apChildren, bool abErrorOnNoObject, ScriptObjectType apImmediateChild)
            {
#elif SKYRIM
            public static bool Prefix(
                ScriptCompiler __instance, ref ScriptObjectType __result,
                string asObjectName, Dictionary<string, ScriptComplexType> akKnownTypes, Stack<string> akChildren, bool abErrorOnNoObject, ScriptObjectType akImmediateChild)
            {

                var apKnownTypes = akKnownTypes;
#endif
                lock (apKnownTypes)
                {
                    if (apKnownTypes.ContainsKey(asObjectName))
                    {
                        __result = apKnownTypes[asObjectName] as ScriptObjectType;
                        return false;
                    }
                }

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
                        lock (apKnownTypes)
                        {
                            if (!apKnownTypes.ContainsKey(asObjectName))
                            {
                                apKnownTypes.Add(asObjectName, externalType);
                            }
                        }

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
#if FALLOUT4
            public static bool Prefix(
                ScriptCompiler __instance,
                ITokenStream apTokenStream, out ScriptObjectType apParsedObj, out IToken arpParentName, out Dictionary<string, IToken> apImports)
            {
#elif SKYRIM
            public static bool Prefix(
                ScriptCompiler __instance,
                ITokenStream akTokenStream, out ScriptObjectType akParsedObj)
            {
                var apTokenStream = akTokenStream;
#endif
                __instance._logger.LogTrace("Parsing {0}...", __instance._targetScript.Id);

                var parser = new PapyrusParser(apTokenStream);

                // TODO: Currently suppressing parser errors so that the compiler will continue with type resolution and disambiguation.
                // AttachParserInternalErrorEventHandler(parser, __instance);

                var parserDynamic = parser.AsDynamic();
                parserDynamic.KnownUserFlags = __instance.GetFlagsDictionary();

                parser.script();

#if FALLOUT4
                apParsedObj = parser.ParsedObject;

                arpParentName = parser.ParentObjName;
                apImports = parser.pImportedItems;
#elif SKYRIM
                akParsedObj = parser.ParsedObject;
#endif

                return false;
            }
        }

        #endregion

#if FALLOUT4
        private static readonly MethodInfo _disambiguateTypeMethod =
            typeof(PCompiler.Compiler).GetMethod("DisambiguateType", BindingFlags.NonPublic | BindingFlags.Instance);
#endif

        private readonly ScriptFile _targetScript;
        private readonly ILogger _logger;

        private readonly dynamic _thisDynamic;

        public ScriptCompiler(ScriptFile targetScript, ILogger logger) : base()
        {
            _thisDynamic = this.AsDynamic();

            _targetScript = targetScript;
            _logger = logger;

#if FALLOUT4
            _thisDynamic.pObjectToPath = new Dictionary<string, string>();
            SetPathForObject(_targetScript.Id, _targetScript.FilePath);
#endif

            CompilerExtensions.SetFlagsDictionary(this, _targetScript.Program.FlagsFile.NativeFlagsDict);
        }

        public ScriptObjectType Load(Dictionary<string, ScriptComplexType> knownTypes)
        {
#if SKYRIM
            lock (_targetScript.Program.ScriptFiles)
            {
                _thisDynamic.kObjectToPath = _targetScript.Program.ScriptFiles.Select(kv => new KeyValuePair<string, string>(kv.Key.ToString().ToLower(), kv.Value.FilePath)).ToDictionary();
            }
#endif

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
                _logger.LogWarning(e, $"Error in LoadObject for {_targetScript.Id}");
            }

            if (result == null)
            {
                return null;
            }

#if FALLOUT4
            if (result.pImportedTypes == null)
            {
                result.pImportedTypes = new Dictionary<string, ScriptObjectType>();
            }

            if (result.pImportedNamespaces == null)
            {
                result.pImportedNamespaces = new HashSet<string>();
            }
#endif

            return result;
        }

#if FALLOUT4
        public string DisambiguateType(string asRawType, IToken apErrorToken, ScriptObjectType apContainingType, Dictionary<string, ScriptComplexType> apKnownTypes)
        {
            return (string)_disambiguateTypeMethod.Invoke(this, new object[] { asRawType, apErrorToken, apContainingType, apKnownTypes });
        }
#endif
    }
}