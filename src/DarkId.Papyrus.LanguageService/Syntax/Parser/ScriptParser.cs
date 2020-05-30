using System;
using System.Collections.Generic;
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
            var tokens = lexer.Tokenize(sourceText);

            _scanner = new Scanner<SyntaxToken>(tokens);
            _scanner.Next();

            return ParseScript();
        }

        private SyntaxToken ConsumeExpected(params SyntaxKind[] kinds)
        {
            var token = _scanner.Peek();

            if (token == null || !kinds.Any(kind => kind == token.Kind))
            {
                token = new SyntaxToken(token == null ? kinds.First() : token.Kind, string.Empty, null, token != null ? new List<GreenNode>() { token } : new List<GreenNode>(), true);
                AddMissingExpectedDiagnostic(token, kinds);
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

        private SyntaxToken ConsumeKind(params SyntaxKind[] kinds)
        {
            var nextKind = _scanner.Peek().Kind;
            if (!_scanner.Done && kinds.Any(kind => kind == nextKind))
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
            var upcoming = _scanner.Peek();
            if (upcoming == null)
            {
                return null;
            }

            switch (upcoming.Kind)
            {
                case SyntaxKind.ImportKeyword:
                    return ParseImport();
                case SyntaxKind.AutoKeyword:
                case SyntaxKind.StateKeyword:
                    return ParseState();
                case SyntaxKind.StructKeyword:
                    return ParseStruct();
                case SyntaxKind.GroupKeyword:
                    return ParseGroup();
                case SyntaxKind.CustomEventKeyword:
                    return ParseCustomEvent();
                case SyntaxKind.EventKeyword:
                    return ParseEvent();
                case SyntaxKind.FunctionKeyword:
                    return ParseFunction();
                case SyntaxKind.IdentifierToken:
                    var followingKind = _scanner.Peek(2)?.Kind;
                    if (followingKind == SyntaxKind.ArrayToken)
                    {
                        followingKind = _scanner.Peek(3)?.Kind;
                    }

                    switch (followingKind)
                    {
                        case SyntaxKind.IdentifierToken:
                            return ParseVariable();
                        case SyntaxKind.PropertyKeyword:
                            return ParseProperty();
                        case SyntaxKind.FunctionKeyword:
                            return ParseFunction();
                        default:
                            _scanner.Next();

                            var invalidIdentifierNode = new UnknownSyntax(_scanner.Current)
                            {
                                TrailingTriviaNodes = ExpectEndOfLine(true)
                            };

                            AddMissingExpectedDiagnostic(invalidIdentifierNode, SyntaxKind.IdentifierToken, SyntaxKind.FunctionKeyword);

                            return invalidIdentifierNode;
                    }
                default:
                    _scanner.Next();

                    var invalidDefinitionNode = new UnknownSyntax(_scanner.Current)
                    {
                        TrailingTriviaNodes = ExpectEndOfLine(true)
                    };

                    AddMissingExpectedDiagnostic(_scanner.Current,
                        SyntaxKind.ImportKeyword,
                        SyntaxKind.AutoKeyword,
                        SyntaxKind.StateKeyword,
                        SyntaxKind.StructKeyword,
                        SyntaxKind.CustomEventKeyword,
                        SyntaxKind.EventKeyword,
                        SyntaxKind.FunctionKeyword,
                        SyntaxKind.IdentifierToken,
                        SyntaxKind.FunctionKeyword);

                    return invalidDefinitionNode;
            }
        }

        private GroupHeaderSyntax ParseGroupHeader()
        {
            var groupKeyword = ConsumeExpected(SyntaxKind.GroupKeyword);
            var identifier = ParseIdentifier();

            return new GroupHeaderSyntax(groupKeyword, identifier)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private GroupDefinitionSyntax ParseGroup()
        {
            var header = ParseGroupHeader();
            var definitions = ParseDefinitions((t) => t.Kind != SyntaxKind.EndGroupKeyword);

            var endGroupKeyword = ConsumeExpected(SyntaxKind.EndGroupKeyword);

            return new GroupDefinitionSyntax(header, definitions, endGroupKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
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

        private List<GreenNode> ParseStatements(Func<SyntaxToken, bool> func)
        {
            var statements = new List<GreenNode>();

            while (!_scanner.Done && func(_scanner.Peek()))
            {
                _scanner.Next();
                ExpectEndOfLine(true);
            }

            return statements;
        }

        private SyntaxToken ConsumeExpectedLiteral()
        {
            return ConsumeExpected(SyntaxKind.TrueKeyword,
                SyntaxKind.FalseKeyword,
                SyntaxKind.NoneKeyword,
                SyntaxKind.IntLiteralToken,
                SyntaxKind.FloatLiteralToken,
                SyntaxKind.HexLiteralToken,
                SyntaxKind.StringLiteral);
        }

        private PropertyHeaderSyntax ParsePropertyHeader()
        {
            var typeIdentifier = ParseTypeIdentifier();
            var propertyKeyword = ConsumeExpected(SyntaxKind.PropertyKeyword);
            var identifier = ParseIdentifier();

            var equalsToken = ConsumeKind(SyntaxKind.EqualsToken);
            var initialValue = equalsToken != null ? ParseLiteralExpression(true) : null;

            var flags = ParseFlags();

            return new PropertyHeaderSyntax(typeIdentifier, propertyKeyword, identifier, equalsToken, initialValue, flags)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private PropertyDefinitionSyntax ParseProperty()
        {
            var header = ParsePropertyHeader();

            if (header.Flags.Any(flag => flag.Kind == SyntaxKind.AutoKeyword || flag.Kind == SyntaxKind.AutoReadOnlyKeyword))
            {
                return new PropertyDefinitionSyntax(header, new List<GreenNode>(), null)
                {
                    TrailingTriviaNodes = ExpectEndOfLine()
                };
            }

            var accessors = ParseDefinitions(t => t.Kind != SyntaxKind.EndPropertyKeyword);
            var endPropertyKeyword = ConsumeExpected(SyntaxKind.EndPropertyKeyword);

            return new PropertyDefinitionSyntax(header, accessors, endPropertyKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
        }

        private VariableDefinitionSyntax ParseVariable()
        {
            var typeIdentifier = ParseTypeIdentifier();
            var identifier = ParseIdentifier();
            var equalsToken = ConsumeKind(SyntaxKind.EqualsToken);
            var initialValue = equalsToken != null ? ParseLiteralExpression(true) : null;
            var flags = ParseFlags();

            return new VariableDefinitionSyntax(typeIdentifier, identifier, equalsToken, initialValue, flags);
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
            var header = ParseFunctionHeader(true);

            if (header.Flags.Any(f => f.Kind == SyntaxKind.NativeKeyword))
            {
                return new EventDefinitionSyntax(header, new List<GreenNode>(), null)
                {
                    TrailingTriviaNodes = ExpectEndOfLine()
                };
            }

            var statements = ParseStatements(token => token.Kind != SyntaxKind.EndEventKeyword);

            var endEventKeyword = ConsumeExpected(SyntaxKind.EndEventKeyword);

            return new EventDefinitionSyntax(header, statements, endEventKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
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

            var statements = ParseStatements(token => token.Kind != SyntaxKind.EndFunctionKeyword);

            var endFunctionKeyword = ConsumeExpected(SyntaxKind.EndFunctionKeyword);

            var func = new FunctionDefinitionSyntax(header, statements, endFunctionKeyword)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };

            if (func.FullText.Contains("added by jdu"))
            {
                Console.Write("wtf");
            }

            return func;
        }

        private FunctionParameterSyntax ParseFunctionParameter()
        {
            var typeIdentifier = ParseTypeIdentifier();
            var identifier = ParseIdentifier();
            var equalsToken = ConsumeKind(SyntaxKind.EqualsToken);
            var defaultValue = equalsToken != null ? ParseLiteralExpression(true) : null;
            var trailingComma = ConsumeKind(SyntaxKind.CommaToken);

            return new FunctionParameterSyntax(typeIdentifier, identifier, equalsToken, defaultValue, trailingComma);
        }

        private IReadOnlyList<FunctionParameterSyntax> ParseFunctionParameters()
        {
            var parameters = new List<FunctionParameterSyntax>();

            if (_scanner.Peek()?.Kind == SyntaxKind.CloseParenToken)
            {
                return parameters;
            }

            while (_scanner.Peek()?.Kind != SyntaxKind.CloseParenToken)
            {
                var parameter = ParseFunctionParameter();
                parameters.Add(parameter);

                if (parameter.TrailingComma == null)
                {
                    break;
                }
            }

            return parameters;
        }

        private FunctionHeaderSyntax ParseFunctionHeader(bool asEvent = false)
        {
            var typeIdentifier = asEvent ? null : _scanner.Peek().Kind == SyntaxKind.FunctionKeyword ? null : ParseTypeIdentifier();

            var functionOrEventKeyword = ConsumeExpected(asEvent ? SyntaxKind.EventKeyword : SyntaxKind.FunctionKeyword);

            var identifier = ParseIdentifier();
            var dotToken = ConsumeKind(SyntaxKind.DotToken);
            if (dotToken != null)
            {
                ParseIdentifier();
            }

            var openParen = ConsumeExpected(SyntaxKind.OpenParenToken);

            var parameters = ParseFunctionParameters();

            var closeParen = ConsumeExpected(SyntaxKind.CloseParenToken);

            var flags = ParseFlags();

            return new FunctionHeaderSyntax(typeIdentifier, functionOrEventKeyword, null, openParen, parameters, closeParen, flags)
            {
                TrailingTriviaNodes = ExpectEndOfLine()
            };
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

        private LiteralExpressionSyntax ParseLiteralExpression(bool allowLeadingNegative = false)
        {
            return new LiteralExpressionSyntax(allowLeadingNegative ? ConsumeKind(SyntaxKind.MinusToken) : null, ConsumeExpectedLiteral());
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
