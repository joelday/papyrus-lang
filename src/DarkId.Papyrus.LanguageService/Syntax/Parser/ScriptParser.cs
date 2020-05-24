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
            var token = _scanner.Peek();

            if (token == null || token.Kind != kind)
            {
                token = new SyntaxToken(kind, string.Empty, null, token != null ? new List<GreenNode>() { token } : new List<GreenNode>(), true);
                AddMissingExpectedDiagnostic(token, kind);
            }

            _scanner.Next();

            return _scanner.Current;
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
            if (!_scanner.Done && _scanner.Peek().Kind == kind)
            {
                _scanner.Next();
                return _scanner.Current;
            }

            return null;
        }

        private IReadOnlyList<SyntaxToken> ConsumeWhile(Func<SyntaxToken, bool> func, bool stopAfterNewLine = true)
        {
            var list = new List<SyntaxToken>();

            while (!_scanner.Done && func(_scanner.Peek()))
            {
                _scanner.Next();
                list.Add(_scanner.Current);

                if (_scanner.Current.TriviaHasNewLine() && stopAfterNewLine)
                {
                    break;
                }
            }

            return list;
        }

        private IReadOnlyList<GreenNode> ExpectEndOfLine(bool ignoreDiagnostic = false)
        {
            if (_scanner.Current.TriviaHasNewLine())
            {
                return new List<GreenNode>();
            }

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

        private GreenNode ParseDefinition()
        {
            switch (_scanner.Peek().Kind)
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
                    switch (_scanner.Peek(2).Kind)
                    {
                        case SyntaxKind.IdentifierToken:
                            return ParseVariable();
                        case SyntaxKind.PropertyKeyword:
                            return ParseProperty();
                        case SyntaxKind.FunctionKeyword:
                            return ParseFunction();
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

        private List<GreenNode> ParseDefinitions(Func<SyntaxToken, bool> func = null)
        {
            var definitions = new List<GreenNode>();

            while (!_scanner.Done && (func == null || func(_scanner.Peek())))
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

        private PropertyDefinitionSyntax ParseProperty()
        {
            var typeIdentifier = ParseTypeIdentifier();
            var propertyKeyword = ConsumeExpected(SyntaxKind.PropertyKeyword);
            var identifier = ParseIdentifier();

            var flags = ParseFlags();

            if (flags.Any(f => f.Kind == SyntaxKind.AutoKeyword || f.Kind == SyntaxKind.AutoReadOnlyKeyword))
            {
                return new PropertyDefinitionSyntax(typeIdentifier, propertyKeyword, identifier, flags, new List<GreenNode>(), null)
                {
                    TrailingTriviaNodes = ExpectEndOfLine()
                };
            }

            var accessors = ParseDefinitions(t => t.Kind != SyntaxKind.EndPropertyKeyword);

            var endPropertyKeyword = ConsumeExpected(SyntaxKind.EndPropertyKeyword);

            return new PropertyDefinitionSyntax(typeIdentifier, propertyKeyword, identifier, flags, accessors, endPropertyKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private VariableDefinitionSyntax ParseVariable()
        {
            ExpectEndOfLine();

            return null;
        }

        private CustomEventDefinitionSyntax ParseCustomEvent()
        {
            var customEventKeyword = ConsumeExpected(SyntaxKind.CustomEventKeyword);
            var identifier = ParseIdentifier();

            return new CustomEventDefinitionSyntax(customEventKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private StructDefinitionSyntax ParseStruct()
        {
            var header = ParseStructHeader();
            var definitions = ParseDefinitions((t) => t.Kind != SyntaxKind.EndStructKeyword);
            var endStructKeyword = ConsumeExpected(SyntaxKind.EndStructKeyword);

            return new StructDefinitionSyntax(header, definitions, endStructKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private StructHeaderSyntax ParseStructHeader()
        {
            var structKeyword = ConsumeExpected(SyntaxKind.StructKeyword);
            var identifier = ParseIdentifier();

            return new StructHeaderSyntax(structKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
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

        private StateDefinitionSyntax ParseState()
        {
            var header = ParseStateHeader();
            var definitions = ParseDefinitions((t) => t.Kind != SyntaxKind.EndStateKeyword);

            var endStateKeyword = ConsumeExpected(SyntaxKind.EndStateKeyword);

            return new StateDefinitionSyntax(header, definitions, endStateKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private ImportSyntax ParseImport()
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
            var header = ParseFunctionHeader();

            if (header.Flags.Any(f => f.Kind == SyntaxKind.NativeKeyword))
            {
                return new FunctionDefinitionSyntax(header, new List<GreenNode>(), null)
                {
                    TrailingTriviaNodes = ExpectEndOfLine()
                };
            }

            var endFunctionKeyword = ConsumeExpected(SyntaxKind.EndFunctionKeyword);

            return new FunctionDefinitionSyntax(header, null, endFunctionKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private FunctionHeaderSyntax ParseFunctionHeader(bool asEvent = false)
        {
            var typeIdentifier = ParseTypeIdentifier();
            var functionKeyword = ConsumeExpected(asEvent ? SyntaxKind.EventKeyword : SyntaxKind.FunctionKeyword);

            // var identifier // Member access expression for events ();

            var openParen = ConsumeExpected(SyntaxKind.OpenParenToken);

            var closeParen = ConsumeExpected(SyntaxKind.CloseParenToken);

            return null;
            // return new FunctionHeaderSyntax(typeIdentifier, functionKeyword, )
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
            if (_scanner.Current.TriviaHasNewLine())
            {
                return new List<SyntaxToken>();
            }

            return ConsumeWhile(t => t.Kind.IsFlagOrIdentifier());
        }
    }
}
