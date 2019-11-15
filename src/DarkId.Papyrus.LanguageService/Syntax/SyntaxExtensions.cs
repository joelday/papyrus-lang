﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public static class SyntaxExtensions
    {
        public static bool IsTrivia(this SyntaxKind kind, bool multiline = false)
        {
            if (kind == SyntaxKind.NewLineTrivia && multiline)
            {
                return true;
            }

            return kind switch
            {
                SyntaxKind.Unknown => true,
                SyntaxKind.DocumentationComment => true,
                SyntaxKind.MultilineComment => true,
                SyntaxKind.SingleLineComment => true,
                SyntaxKind.LineContinuationTrivia => true,
                SyntaxKind.WhitespaceTrivia => true,
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
                kind == SyntaxKind.BetaOnlyKeyword
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
