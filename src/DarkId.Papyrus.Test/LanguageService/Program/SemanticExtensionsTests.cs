using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.Test.LanguageService.Program.TestHarness;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    [TestClass]
    public class SemanticExtensionsTests : ProgramTestBase
    {
        private IEnumerable<PapyrusSymbol> GetReferencableSymbolsAtMarker(
            string marker, bool assertHasResults = true, bool shouldReturnGlobals = false, string script = "ScopeTests")
        {
            var scopeTestScript = Program.ScriptFiles[script];

            var markerPosition = scopeTestScript.GetTestMarker(marker);
            var node = scopeTestScript.Node.GetNodeAtPosition(markerPosition);
            var symbols = node.GetReferencableSymbols();

            Debug.WriteLine($"Referencable symbols: {symbols.Select(s => $"{s.Name} ({s.Kind})").Join(",\r\n")}");

            if (shouldReturnGlobals)
            {
                Assert.IsTrue(symbols.All(s => (s.Flags & LanguageFlags.Global) != 0), "Only globals should be referencable in this case.");
            }
            else
            {
                Assert.IsTrue(symbols.All(s => (s.Flags & LanguageFlags.Global) == 0), "Only non-globals should be referencable in this case.");
            }

            if (assertHasResults)
            {
                Assert.IsTrue(symbols.Count() > 0, "One or more symbols should be referencable in this case.");
            }

            var symbolsWithKindNames = symbols.Select(n => n.Name + n.Kind.ToString());
            CollectionAssert.AreEqual(symbolsWithKindNames.ToList(), symbolsWithKindNames.Distinct().ToList(),
                "Only a single symbol of a given name and kind should be referencable.");

            return symbols;
        }

#if FALLOUT4
        // GetReferencableSymbols in this context only returns structs.
        // The completion handler is responsible for attaching scripts to the completion list.

        [TestMethod]
        public void GetReferencableSymbols_ScriptBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("script-body");
            symbols.AssertAreOfKinds(SymbolKinds.Struct);
        }
#endif

        [TestMethod]
        public void GetReferencableSymbols_FunctionBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("function-body");
            symbols.AssertAreOfKinds(SymbolKinds.Script | SymbolKinds.Struct | SymbolKinds.Function | SymbolKinds.Variable | SymbolKinds.Property);

#if FALLOUT4
            Assert.IsNotNull(symbols.SingleOrDefault(s => s.Name == "GroupProperty"));
#endif
        }

        [TestMethod]
        public void GetReferencableSymbols_NativeFunctionBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("native-function-body", script: "ScriptObject");
            symbols.AssertAreOfKinds(SymbolKinds.Script | SymbolKinds.Struct | SymbolKinds.Function | SymbolKinds.Variable | SymbolKinds.Event | SymbolKinds.Property);
        }

        [TestMethod]
        public void GetReferencableSymbols_GlobalFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("global-function-call", shouldReturnGlobals: true);
            symbols.AssertAreOfKinds(SymbolKinds.Function);
        }

        [TestMethod]
        public void GetReferencableSymbols_ParentFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("parent-function-call");
            symbols.AssertAreOfKinds(SymbolKinds.Function);
        }

        [TestMethod]
        public void GetReferencableSymbols_SelfFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("self-function-call");
            symbols.AssertAreOfKinds(SymbolKinds.Function | SymbolKinds.Property);
        }

        [TestMethod]
        public void GetReferencableSymbols_ArrayMemberAccess()
        {
            var symbols = GetReferencableSymbolsAtMarker("array-member-access");
            symbols.AssertAreOfKinds(SymbolKinds.Function | SymbolKinds.Property);
        }
    }
}
