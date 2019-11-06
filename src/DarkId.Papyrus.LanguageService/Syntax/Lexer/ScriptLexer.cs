using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    public class ScriptLexer
    {
        public static readonly IReadOnlyDictionary<string, SyntaxKind> TokenStringMap =
            new Dictionary<string, SyntaxKind>(StringComparer.OrdinalIgnoreCase)
            {
                {"{", SyntaxKind.OpenBraceToken},
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
                {"[]", SyntaxKind.ArrayToken},
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
                        new[] {NewLineRegex, WhitespaceRegex, IdentifierRegex, HexRegex, FloatRegex, IntRegex}
                            .Select(r => r.ToString())
                            .Concat(TokenStringMap.Keys.OrderBy(k => k.Length).Reverse()
                                .Select(k => Regex.Escape(k).Replace("/", "\\/"))
                            )
                        )}",
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
                FloatRegex.IsMatch(text) ? SyntaxKind.FloatLiteralToken :
                IntRegex.IsMatch(text) ? SyntaxKind.IntLiteralToken :
                SyntaxKind.Unknown;
        }

        public IEnumerable<ScriptToken> Tokenize(string sourceText, ScriptToken? resumeFrom = default)
        {
            var scanner = new Scanner<Match>(TokensRegex.Matches(sourceText, resumeFrom?.Range.End.Value ?? 0));

            if (!scanner.Next())
            {
                yield break;
            }

            var state = resumeFrom?.LexerState ?? new ScriptLexerState()
            {
                StringLiteralStartPosition = -1
            };

            if (resumeFrom.HasValue)
            {
                state.Position = resumeFrom.Value.LexerState.Position + resumeFrom.Value.Text.Length;
                state.PreviousTokenKind = resumeFrom.Value.Kind;
            }

            while (!scanner.Done)
            {
                var text = scanner.Current.Value;

                var nextPosition = state.Position + text.Length;
                var kind = GetTextTokenSyntaxKind(text);

                if (state.ContentState == ScriptLexerContentState.InDocumentationComment)
                {
                    if (kind == SyntaxKind.CloseBraceToken)
                    {
                        state.ContentState = ScriptLexerContentState.InSource;
                    }

                    kind = SyntaxKind.DocumentationCommentContent;
                }
                else if (state.ContentState == ScriptLexerContentState.InMultilineComment)
                {
                    if (kind == SyntaxKind.SlashSemicolonToken)
                    {
                        state.ContentState = ScriptLexerContentState.InSource;
                    }

                    kind = SyntaxKind.MultilineCommentContent;
                }
                else if (state.ContentState == ScriptLexerContentState.InSingleLineComment)
                {
                    if (kind == SyntaxKind.NewLineTrivia)
                    {
                        state.ContentState = ScriptLexerContentState.InSource;
                    }
                    else
                    {
                        kind = SyntaxKind.SingleLineCommentContent;
                    }
                }
                else if (state.ContentState == ScriptLexerContentState.InStringLiteral)
                {
                    if (kind == SyntaxKind.NewLineTrivia)
                    {
                        state.ContentState = ScriptLexerContentState.InSource;

                        //diagnostics.Add(new Diagnostic(DiagnosticLevel.Error, "Unterminated string literal.", new TextRange(
                        //    sourceText.PositionAt(state.StringLiteralStartPosition),
                        //    sourceText.PositionAt(state.Position))));
                    }
                    else if (
                        kind == SyntaxKind.BackslashToken &&
                        state.PreviousTokenKind != SyntaxKind.BackslashToken
                    )
                    {
                        // we know we are escaping something
                        kind = SyntaxKind.StringLiteralContent;

                        if (!scanner.Next())
                        {
                            break;
                        }

                        var nextText = scanner.Current.Value;
                        text += nextText;
                        nextPosition += nextPosition + nextText.Length;
                    }
                    else if (kind == SyntaxKind.DoubleQuoteToken)
                    {
                        state.ContentState = ScriptLexerContentState.InSource;

                        yield return new ScriptToken(
                            kind,
                            text,
                            new Range(state.Position, nextPosition),
                            state
                        );

                        state.PreviousTokenKind = kind;
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
                    case SyntaxKind.OpenBraceToken when (state.ContentState == ScriptLexerContentState.InSource):
                        kind = SyntaxKind.DocumentationCommentContent;
                        state.ContentState = ScriptLexerContentState.InDocumentationComment;
                        break;
                    case SyntaxKind.SemicolonSlashToken when (state.ContentState == ScriptLexerContentState.InSource):
                        kind = SyntaxKind.MultilineCommentContent;
                        state.ContentState = ScriptLexerContentState.InMultilineComment;
                        break;
                    case SyntaxKind.SemicolonToken when (state.ContentState == ScriptLexerContentState.InSource):
                        kind = SyntaxKind.SingleLineCommentContent;
                        state.ContentState = ScriptLexerContentState.InSingleLineComment;
                        break;
                    case SyntaxKind.DoubleQuoteToken when (state.ContentState == ScriptLexerContentState.InSource):
                        state.ContentState = ScriptLexerContentState.InStringLiteral;
                        state.StringLiteralStartPosition = state.Position;
                        break;
                    case SyntaxKind.BackslashToken when (state.ContentState == ScriptLexerContentState.InSource &&
                                                         scanner.Peek() != null &&
                                                         GetTextTokenSyntaxKind(scanner.Peek().Value) == SyntaxKind.NewLineTrivia):
                        kind = SyntaxKind.LineContinuationTrivia;
                        break;
                    case SyntaxKind.NewLineTrivia when (state.ContentState == ScriptLexerContentState.InSource &&
                                                        state.PreviousTokenKind == SyntaxKind.LineContinuationTrivia):
                        kind = SyntaxKind.LineContinuationTrivia;
                        break;
                }

                yield return new ScriptToken(
                    kind,
                    text,
                    new Range(state.Position, nextPosition),
                    state
                );

                state.PreviousTokenKind = kind;
                state.Position = nextPosition;

                scanner.Next();
            }

            yield return new ScriptToken(
                SyntaxKind.EndOfFileToken,
                string.Empty,
                new Range(state.Position, state.Position),
                state
            );
        }
    }
}