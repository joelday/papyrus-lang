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
        private ScriptFile GetScript(string script = "ScopeTests")
        {
            return Program.ScriptFiles[script];
        }

        private IEnumerable<PapyrusSymbol> GetReferencableSymbolsAtMarker(
            string marker, bool beforeMarker = false, bool shouldHaveResults = true, bool shouldReturnGlobals = false, bool canReturnDeclaredGlobals = false, string script = "ScopeTests")
        {
            var testScript = GetScript(script);
            var node = testScript.GetNodeAtMarker(marker, beforeMarker);
            var symbols = node.GetReferencableSymbols();

            Debug.WriteLine($"Referencable symbols: {symbols.Select(s => $"{s.Name} ({s.Kind})").Join(",\r\n")}");

            if (shouldHaveResults)
            {
                Assert.IsTrue(symbols.Count() > 0, "One or more symbols should be referencable in this case.");
            }
            else
            {
                Assert.IsFalse(symbols.Count() > 0, "No symbols should be referencable in this case.");
            }

            if (canReturnDeclaredGlobals)
            {
                var globalSymbols = symbols.Where(s => (s.Flags & LanguageFlags.Global) != 0);
                if (globalSymbols.Any())
                {
                    Assert.IsTrue(globalSymbols.All(s => s.Script.Id == testScript.Id),
                        "Only locally declared globals should be referencable in this case.");
                }
            }
            else if (shouldReturnGlobals)
            {
                Assert.IsTrue(symbols.All(s => (s.Flags & LanguageFlags.Global) != 0), "Only globals should be referencable in this case.");
            }
            else
            {
                Assert.IsTrue(symbols.All(s => (s.Flags & LanguageFlags.Global) == 0), "Only non-globals should be referencable in this case.");
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
            var symbols = GetReferencableSymbolsAtMarker("function-body", canReturnDeclaredGlobals: true);
            symbols.AssertAreOfKinds(SymbolKinds.Script | SymbolKinds.Struct | SymbolKinds.Function | SymbolKinds.Variable | SymbolKinds.Property | SymbolKinds.Event);

            Assert.IsNotNull(symbols.SingleOrDefault(s => s.Name == "LocalGlobalFunction"));
#if FALLOUT4
            Assert.IsNotNull(symbols.SingleOrDefault(s => s.Name == "GroupProperty"));
#endif
        }

        [TestMethod]
        public void GetReferencableSymbols_NativeFunctionBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("native-function-body", script: "ScriptObject", canReturnDeclaredGlobals: true);
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

        [TestMethod]
        public void GetReferencableSymbols_LocalVariableName()
        {
            GetReferencableSymbolsAtMarker("local-variable-name", shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("local-variable-name", true, shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("incomplete-declaration", true, shouldHaveResults: false);
        }

        [TestMethod]
        public void GetReferencableSymbols_FunctionParameterName()
        {
            GetReferencableSymbolsAtMarker("function-parameter-name", shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("function-parameter-name", true, shouldHaveResults: false);
        }

#if FALLOUT4
        [TestMethod]
        public void GetKnownParameterValueSymbols_CustomEvents()
        {
            var script = GetScript();
            var markerPosition = script.GetTestMarker("send-custom-event-param");
            var callExpressionNode = script.Node.GetDescendantNodeOfTypeAtPosition<FunctionCallExpressionNode>(markerPosition);

            var currentParameterIndex = callExpressionNode.GetFunctionParameterIndexAtPosition(markerPosition);

            var symbols = callExpressionNode.GetKnownParameterValueSymbols(currentParameterIndex, out var areValidExclusively);

            Assert.IsTrue(symbols.Count() > 0);
            Assert.IsTrue(symbols.All(s => s is CustomEventSymbol));
        }

        [TestMethod]
        public void GetReferencableSymbols_IncompleteRemoteEvent()
        {
            var script = GetScript();
            GetReferencableSymbolsAtMarker("incomplete-remote-event");
            GetReferencableSymbolsAtMarker("incomplete-remote-event", true);
        }
#endif
    }
}
