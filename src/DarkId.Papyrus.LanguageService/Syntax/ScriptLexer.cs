using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    class ScriptLexer
    {
        private static readonly Dictionary<string, SyntaxKind> TokenStringMap =
            new Dictionary<string, SyntaxKind>(StringComparer.OrdinalIgnoreCase)
            {
                {"{ ", SyntaxKind.OpenBraceToken},
                {"}", SyntaxKind.CloseBraceToken},
                {"(", SyntaxKind.OpenParenToken},
                {")", SyntaxKind.CloseParenToken},
                {"[", SyntaxKind.OpenBracketToken},
                {"]", SyntaxKind.CloseBracketToken},
                {".", SyntaxKind.DotToken},
                {";", SyntaxKind.SemicolonToken},
                {",", SyntaxKind.CommaToken},
                {"<", SyntaxKind.LessThanToken},
                {">", SyntaxKind.GreaterThanToken},
                {"<=", SyntaxKind.LessThanEqualsToken},
                {">=", SyntaxKind.GreaterThanEqualsToken},
                {"==", SyntaxKind.EqualsEqualsToken},
                {"!=", SyntaxKind.ExclamationEqualsToken},
                {"+", SyntaxKind.PlusToken},
                {"-", SyntaxKind.MinusToken},
                {"*", SyntaxKind.AsteriskToken},
                {"/", SyntaxKind.SlashToken},
                {"%", SyntaxKind.PercentToken},
                {"++", SyntaxKind.PlusPlusToken},
                {"--", SyntaxKind.MinusMinusToken},
                {"!", SyntaxKind.ExclamationToken},
                {"&&", SyntaxKind.AmpersandAmpersandToken},
                {"||", SyntaxKind.BarBarToken},
                {"=", SyntaxKind.EqualsToken},
                {"+=", SyntaxKind.PlusEqualsToken},
                {"-=", SyntaxKind.MinusEqualsToken},
                {"*=", SyntaxKind.AsteriskEqualsToken},
                {"/=", SyntaxKind.SlashEqualsToken},
                {"%=", SyntaxKind.PercentEqualsToken},
                {"\\", SyntaxKind.BackslashToken},
                {"\"", SyntaxKind.DoubleQuoteToken},
                {";/", SyntaxKind.SemicolonSlashToken},
                {"/;", SyntaxKind.SlashSemicolonToken},
                {"{}", SyntaxKind.ArrayToken},
                {"as", SyntaxKind.AsKeyword},
                {"auto", SyntaxKind.AutoKeyword},
                {"autoreadonly", SyntaxKind.AutoReadOnlyKeyword},
                {"betaonly", SyntaxKind.BetaOnlyKeyword},
                {"const", SyntaxKind.ConstKeyword},
                {"customevent", SyntaxKind.CustomEventKeyword},
                {"debugonly", SyntaxKind.DebugOnlyKeyword},
                {"else", SyntaxKind.ElseKeyword},
                {"elseif", SyntaxKind.ElseIfKeyword},
                {"endevent", SyntaxKind.EndEventKeyword},
                {"endfunction", SyntaxKind.EndFunctionKeyword},
                {"endgroup", SyntaxKind.EndGroupKeyword},
                {"endif", SyntaxKind.EndIfKeyword},
                {"endproperty", SyntaxKind.EndPropertyKeyword},
                {"endstate", SyntaxKind.EndStateKeyword},
                {"endstruct", SyntaxKind.EndStructKeyword},
                {"endwhile", SyntaxKind.EndWhileKeyword},
                {"event", SyntaxKind.EventKeyword},
                {"extends", SyntaxKind.ExtendsKeyword},
                {"function", SyntaxKind.FunctionKeyword},
                {"global", SyntaxKind.GlobalKeyword},
                {"group", SyntaxKind.GroupKeyword},
                {"if", SyntaxKind.IfKeyword},
                {"import", SyntaxKind.ImportKeyword},
                {"is", SyntaxKind.IsKeyword},
                {"native", SyntaxKind.NativeKeyword},
                {"new", SyntaxKind.NewKeyword},
                {"property", SyntaxKind.PropertyKeyword},
                {"return", SyntaxKind.ReturnKeyword},
                {"scriptname", SyntaxKind.ScriptNameKeyword},
                {"state", SyntaxKind.StateKeyword},
                {"struct", SyntaxKind.StructKeyword},
                {"while", SyntaxKind.WhileKeyword},
                {"true", SyntaxKind.TrueKeyword},
                {"false", SyntaxKind.FalseKeyword},
                {"none", SyntaxKind.NoneKeyword}
            };

        private static readonly Regex NewLineRegex =
            new Regex(@"\r\n|\r|\n", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WhitespaceRegex =
            new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HexRegex =
            new Regex(@"0[x][0-9a-f]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IdentifierRegex =
            new Regex(@"[a-z_]+[:a-z_0-9]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FloatRegex =
            new Regex(@"[0-9]+\.[0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IntRegex = new Regex(@"[0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TokensRegex;

        static ScriptLexer()
        {
            TokensRegex = new Regex(
                $@"{
                    string.Join('|',
                        new[] {NewLineRegex, WhitespaceRegex, IdentifierRegex, HexRegex, FloatRegex}
                            .Select(r => r.ToString())
                            .Concat(TokenStringMap.Keys.OrderBy(k => k.Length).Reverse().Select(Regex.Escape)))}|*",
                RegexOptions.Compiled | RegexOptions.IgnoreCase
            );
        }

        private static SyntaxKind GetTextTokenSyntaxKind(string text)
        {
            return NewLineRegex.IsMatch(text) ? SyntaxKind.NewLineTrivia :
                WhitespaceRegex.IsMatch(text) ? SyntaxKind.WhitespaceTrivia :
                TokenStringMap.ContainsKey(text) ? TokenStringMap[text] :
                HexRegex.IsMatch(text) ? SyntaxKind.HexLiteralToken :
                IdentifierRegex.IsMatch(text) ? SyntaxKind.IdentifierToken :
                IntRegex.IsMatch(text) ? SyntaxKind.IntLiteralToken :
                SyntaxKind.Unknown;
        }

        public IEnumerable<ScriptToken> Tokenize(ScriptText sourceText, DiagnosticsContext diagnostics)
        {
            var scanner = new Scanner<string>(TokensRegex.Matches(sourceText.Text).Select(t => t.Value));

            if (!scanner.Next())
            {
                yield break;
            }

            var state = new ScriptLexerState()
            {
                StringLiteralStartPosition = -1
            };

            while (!scanner.Done)
            {
                var text = scanner.Current;

                var nextPosition = state.Position + text.Length;
                var kind = GetTextTokenSyntaxKind(text);

                if ((state.Flags & ScriptLexerStateFlags.InDocumentationComment) != 0)
                {
                    if (kind == SyntaxKind.CloseBraceToken)
                    {
                        state.Flags &= ~ScriptLexerStateFlags.InDocumentationComment;
                    }
                    else
                    {
                        kind = SyntaxKind.DocumentationCommentTrivia;
                    }
                }
                else if ((state.Flags & ScriptLexerStateFlags.InMultilineComment) != 0)
                {
                    if (kind == SyntaxKind.SlashSemicolonToken)
                    {
                        state.Flags &= ~ScriptLexerStateFlags.InMultilineComment;
                    }
                    else
                    {
                        kind = SyntaxKind.MultilineCommentTrivia;
                    }
                }
                else if ((state.Flags & ScriptLexerStateFlags.InSingleLineComment) != 0)
                {
                    if (kind == SyntaxKind.NewLineTrivia)
                    {
                        state.Flags &= ~ScriptLexerStateFlags.InSingleLineComment;
                    }
                    else
                    {
                        kind = SyntaxKind.SingleLineCommentTrivia;
                    }
                }
                else if ((state.Flags & ScriptLexerStateFlags.InStringLiteral) != 0)
                {
                    if (kind == SyntaxKind.NewLineTrivia)
                    {
                        state.Flags &= ~ScriptLexerStateFlags.InStringLiteral;
                        diagnostics.Add(new Diagnostic(DiagnosticLevel.Error, "Unterminated string literal.", new TextRange(
                            sourceText.PositionAt(state.StringLiteralStartPosition),
                            sourceText.PositionAt(state.Position))));
                    }
                    else if (
                        kind == SyntaxKind.BackslashToken &&
                        GetTextTokenSyntaxKind(state.PreviousText) != SyntaxKind.BackslashToken
                    )
                    {
                        // we know we are escaping something
                        kind = SyntaxKind.StringLiteralContent;

                        if (!scanner.Next())
                        {
                            break;
                        }

                        var nextText = scanner.Current;
                        text += nextText;
                        nextPosition += nextPosition + nextText.Length;
                    }
                    else if (kind == SyntaxKind.DoubleQuoteToken)
                    {
                        state.Flags &= ~ScriptLexerStateFlags.InStringLiteral;

                        yield return new ScriptToken(
                            kind,
                            text,
                            new TextRange(sourceText.PositionAt(state.Position), sourceText.PositionAt(nextPosition)),
                            state
                        );

                        state.PreviousText = text;
                        state.Position = nextPosition;

                        scanner.Next();

                        continue;
                    }
                    else
                    {
                        kind = SyntaxKind.StringLiteralContent;
                    }
                }

                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (kind)
                {
                    case SyntaxKind.OpenBraceToken
                        when (state.Flags & ScriptLexerStateFlags.InSingleLineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InMultilineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InStringLiteral) == 0:
                        state.Flags &= ScriptLexerStateFlags.InDocumentationComment;
                        break;
                    case SyntaxKind.SemicolonSlashToken
                        when (state.Flags & ScriptLexerStateFlags.InSingleLineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InStringLiteral) == 0:
                        state.Flags &= ScriptLexerStateFlags.InMultilineComment;
                        break;
                    case SyntaxKind.SemicolonToken
                        when (state.Flags & ScriptLexerStateFlags.InDocumentationComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InMultilineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InStringLiteral) == 0:
                        state.Flags &= ScriptLexerStateFlags.InSingleLineComment;
                        break;
                    case SyntaxKind.DoubleQuoteToken
                        when (state.Flags & ScriptLexerStateFlags.InDocumentationComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InMultilineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InSingleLineComment) == 0 &&
                             (state.Flags & ScriptLexerStateFlags.InStringLiteral) == 0:
                        state.Flags &= ScriptLexerStateFlags.InStringLiteral;
                        state.StringLiteralStartPosition = state.Position;
                        break;
                }

                yield return new ScriptToken(
                    kind,
                    text,
                    new TextRange(sourceText.PositionAt(state.Position), sourceText.PositionAt(nextPosition)),
                    state
                );

                state.PreviousText = text;
                state.Position = nextPosition;

                scanner.Next();
            }
        }
    }
}