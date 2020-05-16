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
        private Scanner<SyntaxToken> _scanner;

        public ScriptSyntax Parse(string sourceText, LanguageVersion languageVersion)
        {
            var lexer = new ScriptLexer();
            var tokens = lexer.Tokenize(sourceText).ToInlinedTriviaTokens();

            _scanner = new Scanner<SyntaxToken>(tokens);
            _scanner.Next();

            return ParseScript();
        }

        private SyntaxToken CurrentToken => _scanner.Current;

        private SyntaxToken EatToken()
        {
            var current = CurrentToken;
            MoveToNextToken();
            return current;
        }

        private SyntaxToken EatToken(SyntaxKind kind)
        {
            var current = CurrentToken;

            if (current.Kind == kind)
            {
                MoveToNextToken();
                return current;
            }

            return CreateMissingToken(kind, current.Kind, true);
        }

        private void MoveToNextToken()
        {
            _scanner.Next();
        }

        private SyntaxToken CreateMissingToken(SyntaxKind expected, SyntaxKind actual, bool reportError)
        {
            var token = new SyntaxToken(expected, string.Empty, isMissing: true);

            return token;
        }

        private SyntaxToken ConsumeExpected(SyntaxKind kind)
        {
            var token = _scanner.Current;

            if (token == null || token.Kind != kind)
            {
                token = new SyntaxToken(kind, string.Empty, null, token != null ? new List<GreenNode>() { token } : new List<GreenNode>(), true);
                AddMissingExpectedDiagnostic(token, kind);
            }

            _scanner.Next();

            return token;
        }

        private void AddMissingExpectedDiagnostic(GreenNode node, params SyntaxKind[] kinds)
        {
            var expected = kinds.Select(kind => ScriptLexer.StringTokenMap.ContainsKey(kind)
                ? ScriptLexer.StringTokenMap[kind]
                : nameof(kind)).Join(" | ");

            node.AddDiagnostic(new DiagnosticInfo(DiagnosticLevel.Error, 1001, $"Expected {expected}."));
        }

        private SyntaxToken ConsumeKind(SyntaxKind kind)
        {
            var current = _scanner.Current;
            if (current != null && current.Kind == kind)
            {
                _scanner.Next();
                return current;
            }

            return null;
        }

        private IReadOnlyList<SyntaxToken> ConsumeWhile(Func<SyntaxToken, bool> func)
        {
            var list = new List<SyntaxToken>();

            while (!_scanner.Done && func(_scanner.Current))
            {
                list.Add(_scanner.Current);
                _scanner.Next();
            }

            return list;
        }

        private IReadOnlyList<GreenNode> ExpectEndOfLine(bool ignoreDiagnostic = false)
        {
            // if (_scanner.PeekPrevious().TriviaHasNewLine())
            // {
            //     return new List<GreenNode>();
            // }

            var restOfLine = ConsumeWhile(token => !token.TriviaHasNewLine()).ToList();

            if (!ignoreDiagnostic)
            {
                restOfLine.FirstOrDefault()?.AddDiagnostic(new DiagnosticInfo(DiagnosticLevel.Error, 1002, "Expected end of line."));
            }

            return restOfLine;
        }


        private ScriptSyntax ParseScript()
        {
            var header = ParseScriptHeader();
            var definitions = ParseDefinitions();

            return new ScriptSyntax(header, definitions.ToList());
        }

        private ScriptHeaderSyntax ParseScriptHeader()
        {
            var scriptNameKeyword = ConsumeExpected(SyntaxKind.ScriptNameKeyword);
            var identifier = ParseIdentifier();

            var extendsKeyword = ConsumeKind(SyntaxKind.ExtendsKeyword);
            var extended = extendsKeyword != null ? ParseTypeIdentifier() : null;

            var flags = ParseFlags();

            return new ScriptHeaderSyntax(scriptNameKeyword, identifier, extendsKeyword, extended, flags)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private GreenNode ParseDefinition() {
            switch (_scanner.Current.Kind)
                {
                    case SyntaxKind.ImportKeyword:
                        return ParseImport();
                    case SyntaxKind.AutoKeyword:
                    case SyntaxKind.StateKeyword:
                        return ParseState();
                    case SyntaxKind.StructKeyword:
                        return ParseStruct();
                    case SyntaxKind.CustomEventKeyword:
                        return ParseCustomEvent();
                    case SyntaxKind.EventKeyword:
                        return ParseEvent();
                    case SyntaxKind.FunctionKeyword:
                        return ParseFunction();
                    case SyntaxKind.IdentifierToken:
                        switch (_scanner.Peek().Kind)
                        {
                            case SyntaxKind.IdentifierToken:
                                return ParseVariable();
                                break;
                            case SyntaxKind.PropertyKeyword:
                                return ParseProperty();
                                break;
                            case SyntaxKind.FunctionKeyword:
                                return ParseFunction();
                                break;
                            default:
                                var currentIdentifier = _scanner.Current;
                                _scanner.Next();

                                // var identifierErrorNode = new ErrorSyntax(currentIdentifier)
                                // {
                                //     TrailingTriviaNodes = ExpectEndOfLine(true)
                                // };

                                // AddMissingExpectedDiagnostic(identifierErrorNode, SyntaxKind.IdentifierToken, SyntaxKind.FunctionKeyword);
                                // definitions.Add(identifierErrorNode);

                                break;
                        }

                        break;
                    default:
                        var current = _scanner.Current;
                        _scanner.Next();

                        // var errorNode = new ErrorSyntax(current)
                        // {
                        //     TrailingTriviaNodes = ExpectEndOfLine(true)
                        // };

                        // AddMissingExpectedDiagnostic(errorNode,
                        //     SyntaxKind.ImportKeyword,
                        //     SyntaxKind.AutoKeyword,
                        //     SyntaxKind.StateKeyword,
                        //     SyntaxKind.StructKeyword,
                        //     SyntaxKind.CustomEventKeyword,
                        //     SyntaxKind.EventKeyword,
                        //     SyntaxKind.FunctionKeyword,
                        //     SyntaxKind.IdentifierToken,
                        //     SyntaxKind.FunctionKeyword);

                        // definitions.Add(errorNode);

                        break;
                }

                return null;
        }

        private List<GreenNode> ParseDefinitions()
        {
            var definitions = new List<GreenNode>();

            while (!_scanner.Done)
            {
                var definition = ParseDefinition();
                if (definition == null)
                {
                    break;
                }

                definitions.Add(definition);
            }

            return definitions;
        }

        private GreenNode ParseProperty()
        {
            ExpectEndOfLine();

            return null;
        }

        private GreenNode ParseVariable()
        {
            ExpectEndOfLine();

            return null;
        }

        private GreenNode ParseCustomEvent()
        {
            var customEventKeyword = ConsumeExpected(SyntaxKind.CustomEventKeyword);
            var identifier = ParseIdentifier();

            return new CustomEventDefinitionSyntax(customEventKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private GreenNode ParseStruct()
        {
            ExpectEndOfLine();

            return null;
        }

        private StateHeaderSyntax ParseStateHeader()
        {
            var autoKeyword = ConsumeKind(SyntaxKind.AutoKeyword);
            var stateKeyword = ConsumeExpected(SyntaxKind.StateKeyword);
            var identifier = ParseIdentifier();

            return new StateHeaderSyntax(autoKeyword, stateKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private GreenNode ParseState()
        {
            var header = ParseStateHeader();
            var definitions = ParseDefinitions(); //ParseDefinitions(() => _scanner.Current.Kind != SyntaxKind.EndStateKeyword);

            var endStateKeyword = ConsumeExpected(SyntaxKind.EndStateKeyword);

            return new StateDefinitionSyntax(header, definitions, endStateKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private GreenNode ParseImport()
        {
            var importKeyword = ConsumeExpected(SyntaxKind.ImportKeyword);
            var identifier = ParseIdentifier();

            return new ImportSyntax(importKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private EventDefinitionSyntax ParseEvent()
        {
            ExpectEndOfLine();

            return null;
        }

        private FunctionDefinitionSyntax ParseFunction()
        {
            ExpectEndOfLine();

            return null;
        }

        private IdentifierSyntax ParseIdentifier()
        {
            return new IdentifierSyntax(ConsumeExpected(SyntaxKind.IdentifierToken));
        }

        private TypeIdentifierSyntax ParseTypeIdentifier()
        {
            var identifier = ParseIdentifier();

            return new TypeIdentifierSyntax(identifier, ConsumeKind(SyntaxKind.ArrayToken));
        }

        private IReadOnlyList<SyntaxToken> ParseFlags()
        {
            return ConsumeWhile(t => t.Kind.IsFlagOrIdentifier());
        }
    }
}
