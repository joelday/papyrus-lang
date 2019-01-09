using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService
{
    public static class HarmonyPatches
    {
        public static void Apply()
        {
            var harmony = HarmonyInstance.Create("PapyrusHarmonyPatch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
