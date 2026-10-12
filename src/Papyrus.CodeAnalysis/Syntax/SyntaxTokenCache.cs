using System;
using System.Collections.Concurrent;
using Papyrus.CodeAnalysis.Syntax.InternalSyntax;

namespace Papyrus.CodeAnalysis.Syntax;

internal static class SyntaxTokenCache
{
    private const int MaxCachedLength = 42;

    private static readonly ConcurrentDictionary<CacheKey, GreenToken> Cache = new();

    internal static GreenToken Get(
        SyntaxKind kind,
        string text,
        GreenNode? leading,
        GreenNode? trailing,
        bool isMissing)
    {
        if (isMissing || leading is not null || trailing is not null || text.Length > MaxCachedLength)
        {
            return new GreenToken(kind, text, leading, trailing, isMissing);
        }

        return Cache.GetOrAdd(new CacheKey(kind, text), static key =>
            new GreenToken(key.Kind, key.Text, leading: null, trailing: null, isMissing: false));
    }

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        internal CacheKey(SyntaxKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        internal SyntaxKind Kind { get; }

        internal string Text { get; }

        public bool Equals(CacheKey other)
            => Kind == other.Kind && string.Equals(Text, other.Text, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, StringComparer.Ordinal.GetHashCode(Text));
    }
}
