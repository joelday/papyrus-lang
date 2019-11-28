using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Syntax.InternalSyntax;
using DarkId.Papyrus.LanguageService.Syntax.Lexer;

namespace DarkId.Papyrus.LanguageService.Syntax.Parser
{
    internal class ScriptParser
    {
        private Scanner<Scanner<SyntaxToken>> _scanner;

        private Scanner<SyntaxToken> CurrentLine => _scanner.Current;
        private SyntaxToken CurrentToken => _scanner.Current.Current;

        private bool MoveToNextLine()
        {
            return _scanner.Next();
        }

        private bool MoveToNextToken()
        {
            return !_scanner.Done && _scanner.Current.Next();
        }

        public SyntaxToken Expect(SyntaxKind kind)
        {
            if (CurrentLine.PeekDone || CurrentLine.Peek().Kind != kind)
            {
                var expected = ScriptLexer.StringTokenMap.ContainsKey(kind)
                    ? ScriptLexer.StringTokenMap[kind]
                    : nameof(kind);

                if (!CurrentLine.PeekDone)
                {
                    MoveToNextToken();
                    CurrentToken.AddDiagnostic(new DiagnosticInfo(DiagnosticLevel.Error, 1001, $"Expected {expected}."));
                }

                return new SyntaxToken(kind, string.Empty)
                {
                    IsMissing = true
                };
            }

            MoveToNextToken();
            return CurrentToken;
        }

        public SyntaxToken ConsumeKind(SyntaxKind kind)
        {
            if (CurrentLine.PeekDone || CurrentLine.Peek().Kind != kind)
            {
                return null;
            }

            MoveToNextToken();
            return CurrentToken;
        }

        public IReadOnlyList<SyntaxToken> ConsumeWhile(Func<SyntaxToken, bool> func)
        {
            var list = new List<SyntaxToken>();

            while (!CurrentLine.PeekDone && func(CurrentLine.Peek()))
            {
                MoveToNextToken();
                list.Add(CurrentToken);
            }

            return list;
        }

        public List<GreenNode> ExpectEndOfLine()
        {
            var restOfLine = CurrentLine.AllRemaining().Cast<GreenNode>().ToList();
            foreach (var remaining in restOfLine)
            {
                remaining.AddDiagnostic(new DiagnosticInfo(DiagnosticLevel.Error, 1002, "Expected end of line."));
            }

            return restOfLine;
        }

        public ScriptSyntax Parse(string sourceText, LanguageVersion languageVersion)
        {
            var lexer = new ScriptLexer();
            var lines = lexer.Tokenize(sourceText).ToLogicalLines();

            _scanner = new Scanner<Scanner<SyntaxToken>>(lines.Select(line => new Scanner<SyntaxToken>(line)));
            _scanner.Next();

            return ParseScript();
        }

        private ScriptSyntax ParseScript()
        {
            var header = ParseScriptHeader();
            var definitions = ParseDefinitions();

            return new ScriptSyntax(header, definitions.ToList());
        }

        private ScriptHeaderSyntax ParseScriptHeader()
        {
            var scriptNameKeyword = Expect(SyntaxKind.ScriptNameKeyword);
            var identifier = ParseIdentifier();

            var extendsKeyword = ConsumeKind(SyntaxKind.ExtendsKeyword);
            var extended = extendsKeyword != null ? ParseTypeIdentifier() : null;

            var flags = ParseFlags();

            return new ScriptHeaderSyntax(scriptNameKeyword, identifier, extendsKeyword, extended, flags.ToList())
            {
                TrailingTriviaTokens = ExpectEndOfLine()
            };
        }

        private List<GreenNode> ParseDefinitions()
        {
            var definitions = new List<GreenNode>();

            // Need to handle single upcoming line.
            while (MoveToNextLine())
            {
                switch (CurrentLine.Peek().Kind)
                {
                    case SyntaxKind.ImportKeyword:
                        definitions.Add(ParseImport());
                        break;
                    case SyntaxKind.AutoKeyword:
                    case SyntaxKind.StateKeyword:
                        definitions.Add(ParseState());
                        break;
                    case SyntaxKind.StructKeyword:
                        definitions.Add(ParseStruct());
                        break;
                    case SyntaxKind.CustomEventKeyword:
                        definitions.Add(ParseCustomEvent());
                        break;
                    case SyntaxKind.EventDefinition:
                        definitions.Add(ParseEvent());
                        break;
                    case SyntaxKind.FunctionDefinition:
                        definitions.Add(ParseFunction());
                        break;
                    case SyntaxKind.Identifier:
                        break;
                    default:
                        break;
                }
            }

            return definitions;
        }

        private GreenNode ParseCustomEvent()
        {
            return null;
        }

        private GreenNode ParseStruct()
        {
            return null;
        }

        private StateHeaderSyntax ParseStateHeader()
        {
            var autoKeyword = ConsumeKind(SyntaxKind.AutoKeyword);
            var stateKeyword = Expect(SyntaxKind.StateKeyword);
            var identifier = ParseIdentifier();

            return new StateHeaderSyntax(autoKeyword, stateKeyword, identifier)
            {
                TrailingTriviaTokens = ExpectEndOfLine()
            };
        }

        private GreenNode ParseState()
        {
            var header = ParseStateHeader();
            var definitions = ParseDefinitions();

            MoveToNextLine();
            var endStateKeyword = Expect(SyntaxKind.EndStateKeyword);

            return new StateDefinitionSyntax(header, definitions, endStateKeyword)
            {
                TrailingTriviaTokens = ExpectEndOfLine()
            };
        }

        private GreenNode ParseImport()
        {
            var importKeyword = Expect(SyntaxKind.ImportKeyword);
            var identifier = ParseIdentifier();

            return new ImportSyntax(importKeyword, identifier)
            {
                TrailingTriviaTokens = ExpectEndOfLine()
            };
        }

        private EventDefinitionSyntax ParseEvent()
        {
            return null;
        }

        private FunctionDefinitionSyntax ParseFunction()
        {
            return null;
        }

        private IdentifierSyntax ParseIdentifier()
        {
            return new IdentifierSyntax(Expect(SyntaxKind.IdentifierToken));
        }

        private TypeIdentifierSyntax ParseTypeIdentifier()
        {
            var identifier = ParseIdentifier();

            return new TypeIdentifierSyntax(identifier, ConsumeKind(SyntaxKind.ArrayToken));
        }

        private IEnumerable<SyntaxToken> ParseFlags()
        {
            return ConsumeWhile(t => t.Kind.IsFlagOrIdentifier());
        }
    }
}
