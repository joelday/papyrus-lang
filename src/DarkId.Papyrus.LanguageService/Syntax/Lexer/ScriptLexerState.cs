using System;

namespace DarkId.Papyrus.LanguageService.Syntax.Lexer
{
    internal struct ScriptLexerState
    {
        public ScriptLexerContentState ContentState { get; set; }
        public int Position { get; set; }
        public int StringLiteralStartPosition { get; set; }
        public SyntaxKind PreviousTokenKind { get; set; }
        public bool AtUnterminatedStringLiteral { get; set; }
    }
}