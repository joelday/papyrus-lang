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

namespace DarkId.Papyrus.LanguageService.Compiler
{
    [HarmonyPatch(typeof(ANTLRFileStream), "Load")]
    public static class AntlrPatch
    {
        private static IScriptTextProvider _textProvider;

        public static void SetTextProvider(IScriptTextProvider textProvider)
        {
            _textProvider = textProvider;
        }

        public static bool Prefix(ANTLRFileStream __instance, string fileName, Encoding encoding)
		{
            var asDynamic = __instance.AsDynamic();
            asDynamic.data = _textProvider.GetText(fileName).WaitForResult().Text.ToCharArray();
            asDynamic.n = asDynamic.data.Length;

            return false;
		}
    }
}