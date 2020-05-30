using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public static class SyntaxExtensions
    {
        public static bool IsNonStructuredTrivia(this SyntaxKind kind)
        {
            return !kind.IsStructuredTrivia() && kind.IsTrivia();
        }

        public static bool IsTrivia(this SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.Unknown => true,
                SyntaxKind.NewLineTrivia => true,
                SyntaxKind.DocumentationComment => true,
                SyntaxKind.MultilineComment => true,
                SyntaxKind.SingleLineComment => true,
                SyntaxKind.LineContinuationTrivia => true,
                SyntaxKind.WhitespaceTrivia => true,
                SyntaxKind.EndOfFileToken => true,
                _ => false
            };
        }

        public static bool IsStructuredTrivia(this SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.DocumentationComment => true,
                SyntaxKind.MultilineComment => true,
                SyntaxKind.SingleLineComment => true,
                _ => false
            };
        }

        public static bool IsComparisonOperator(this SyntaxKind kind)
        {
            return (
                kind == SyntaxKind.EqualsToken ||
                kind == SyntaxKind.PlusEqualsToken ||
                kind == SyntaxKind.MinusEqualsToken ||
                kind == SyntaxKind.SlashEqualsToken ||
                kind == SyntaxKind.AsteriskEqualsToken ||
                kind == SyntaxKind.PercentEqualsToken
            );
        }

        public static bool IsNativeFlag(this SyntaxKind kind)
        {
            return (
                kind == SyntaxKind.NativeKeyword ||
                kind == SyntaxKind.DebugOnlyKeyword ||
                kind == SyntaxKind.BetaOnlyKeyword ||
                kind == SyntaxKind.ConstKeyword ||
                kind == SyntaxKind.AutoReadOnlyKeyword ||
                kind == SyntaxKind.AutoKeyword ||
                kind == SyntaxKind.GlobalKeyword
            );
        }

        public static bool IsLiteralToken(this SyntaxKind kind)
        {
            return (
                kind == SyntaxKind.TrueKeyword ||
                kind == SyntaxKind.FalseKeyword ||
                kind == SyntaxKind.NoneKeyword ||
                kind == SyntaxKind.IntLiteralToken ||
                kind == SyntaxKind.FloatLiteralToken ||
                kind == SyntaxKind.HexLiteralToken
            );
        }

        public static bool IsFlagOrIdentifier(this SyntaxKind kind)
        {
            return kind.IsNativeFlag() || kind == SyntaxKind.IdentifierToken;
        }

        public static bool IsBinaryOperator(this SyntaxKind kind)
        {
            return (
                kind == SyntaxKind.EqualsEqualsToken ||
                kind == SyntaxKind.ExclamationEqualsToken ||
                kind == SyntaxKind.GreaterThanToken ||
                kind == SyntaxKind.GreaterThanEqualsToken ||
                kind == SyntaxKind.LessThanToken ||
                kind == SyntaxKind.LessThanEqualsToken
            );
        }

        public static AssignmentOperatorType ToAssignmentOperator(this SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.EqualsToken => AssignmentOperatorType.Assign,
                SyntaxKind.PlusEqualsToken => AssignmentOperatorType.Add,
                SyntaxKind.MinusEqualsToken => AssignmentOperatorType.Subtract,
                SyntaxKind.SlashEqualsToken => AssignmentOperatorType.Divide,
                SyntaxKind.AsteriskEqualsToken => AssignmentOperatorType.Multiply,
                SyntaxKind.PercentEqualsToken => AssignmentOperatorType.Modulus,
                _ => AssignmentOperatorType.None
            };
        }

        public static BinaryOperatorType ToComparisonOperator(this SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.EqualsEqualsToken => BinaryOperatorType.CompareEqual,
                SyntaxKind.ExclamationEqualsToken => BinaryOperatorType.CompareNotEqual,
                SyntaxKind.GreaterThanToken => BinaryOperatorType.CompareGreaterThan,
                SyntaxKind.GreaterThanEqualsToken => BinaryOperatorType.CompareGreaterThanOrEqual,
                SyntaxKind.LessThanToken => BinaryOperatorType.CompareLessThan,
                SyntaxKind.LessThanEqualsToken => BinaryOperatorType.CompareLessThanOrEqual,
                _ => BinaryOperatorType.None
            };
        }
    }
}
