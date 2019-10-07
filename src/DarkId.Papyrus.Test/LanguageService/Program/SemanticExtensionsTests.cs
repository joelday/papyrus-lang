using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService;
using DarkId.Papyrus.LanguageService.Program;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService.Program
{
    public class SemanticExtensionsTests : ProgramTestBase
    {
        public SemanticExtensionsTests(PapyrusProgram program) : base(program)
        {
        }

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

            var referencableSymbols = symbols.ToList();
            Debug.WriteLine($"Referencable symbols: {referencableSymbols.Select(s => $"{s.Name} ({s.Kind})").Join(",\r\n")}");

            if (shouldHaveResults)
            {
                Assert.IsTrue(referencableSymbols.Any(), "One or more symbols should be referencable in this case.");
            }
            else
            {
                Assert.IsFalse(referencableSymbols.Any(), "No symbols should be referencable in this case.");
            }

            if (canReturnDeclaredGlobals)
            {
                var globalSymbols = referencableSymbols.Where(s => (s.Flags & LanguageFlags.Global) != 0).ToList();
                if (globalSymbols.Any())
                {
                    Assert.IsTrue(globalSymbols.All(s => s.Script.Id == testScript.Id),
                        "Only locally declared globals should be referencable in this case.");
                }
            }
            else if (shouldReturnGlobals)
            {
                Assert.IsTrue(referencableSymbols.All(s => (s.Flags & LanguageFlags.Global) != 0), "Only globals should be referencable in this case.");
            }
            else
            {
                Assert.IsTrue(referencableSymbols.All(s => (s.Flags & LanguageFlags.Global) == 0), "Only non-globals should be referencable in this case.");
            }

            var symbolsWithKindNames = referencableSymbols.Select(n => n.Name + n.Kind).ToList();
            CollectionAssert.AreEqual(symbolsWithKindNames.ToList(), symbolsWithKindNames.Distinct().ToList(),
                "Only a single symbol of a given name and kind should be referencable.");

            return referencableSymbols;
        }

        // GetReferencableSymbols in this context only returns structs.
        // The completion handler is responsible for attaching scripts to the completion list.

        [Test]
        [LanguageVersions(LanguageVersion.Fallout4)]
        public void GetReferencableSymbols_ScriptBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("script-body");
            symbols.AssertAreOfKinds(SymbolKinds.Struct);
        }

        [Test]
        public void GetReferencableSymbols_FunctionBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("function-body", canReturnDeclaredGlobals: true);
            symbols.AssertAreOfKinds(SymbolKinds.Script | SymbolKinds.Struct | SymbolKinds.Function | SymbolKinds.Variable | SymbolKinds.Property | SymbolKinds.Event);

            
            Assert.IsNotNull(symbols.SingleOrDefault(s => s.Name == "LocalGlobalFunction"));
           
            if (Program.Options.LanguageVersion == LanguageVersion.Fallout4)
            {
                Assert.IsNotNull(symbols.SingleOrDefault(s => s.Name == "GroupProperty"));
            }
        }

        [Test]
        public void GetReferencableSymbols_NativeFunctionBody()
        {
            var symbols = GetReferencableSymbolsAtMarker("native-function-body", script: "ScriptObject", canReturnDeclaredGlobals: true);
            symbols.AssertAreOfKinds(SymbolKinds.Script | SymbolKinds.Struct | SymbolKinds.Function | SymbolKinds.Variable | SymbolKinds.Event | SymbolKinds.Property);
        }

        [Test]
        public void GetReferencableSymbols_GlobalFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("global-function-call", shouldReturnGlobals: true);
            symbols.AssertAreOfKinds(SymbolKinds.Function);
        }

        [Test]
        public void GetReferencableSymbols_ParentFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("parent-function-call");
            symbols.AssertAreOfKinds(SymbolKinds.Function | SymbolKinds.Event);
        }

        [Test]
        public void GetReferencableSymbols_SelfFunctionCall()
        {
            var symbols = GetReferencableSymbolsAtMarker("self-function-call");
            symbols.AssertAreOfKinds(SymbolKinds.Function | SymbolKinds.Event | SymbolKinds.Property);
        }

        [Test]
        public void GetReferencableSymbols_ArrayMemberAccess()
        {
            var symbols = GetReferencableSymbolsAtMarker("array-member-access");
            symbols.AssertAreOfKinds(SymbolKinds.Function | SymbolKinds.Property);
        }

        [Test]
        public void GetReferencableSymbols_LocalVariableName()
        {
            GetReferencableSymbolsAtMarker("local-variable-name", shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("local-variable-name", true, shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("incomplete-declaration", true, shouldHaveResults: false);
        }

        [Test]
        public void GetReferencableSymbols_FunctionParameterName()
        {
            GetReferencableSymbolsAtMarker("function-parameter-name", shouldHaveResults: false);
            GetReferencableSymbolsAtMarker("function-parameter-name", true, shouldHaveResults: false);
        }

        [Test]
        [LanguageVersions(LanguageVersion.Fallout4)]
        public void GetKnownParameterValueSymbols_CustomEvents()
        {
            var script = GetScript();
            var markerPosition = script.GetTestMarker("send-custom-event-param");
            var callExpressionNode = script.Node.GetDescendantNodeOfTypeAtPosition<FunctionCallExpressionNode>(markerPosition);

            var currentParameterIndex = callExpressionNode.GetFunctionParameterIndexAtPosition(markerPosition);

            var symbols = callExpressionNode.GetKnownParameterValueSymbols(currentParameterIndex, out var areValidExclusively);

            Assert.IsTrue(symbols.Any());
            Assert.IsTrue(symbols.All(s => s is CustomEventSymbol));
        }

        [Test]
        [LanguageVersions(LanguageVersion.Fallout4)]
        public void GetReferencableSymbols_IncompleteRemoteEvent()
        {
            GetScript();
            GetReferencableSymbolsAtMarker("incomplete-remote-event");
            GetReferencableSymbolsAtMarker("incomplete-remote-event", true);
        }
    }
}
