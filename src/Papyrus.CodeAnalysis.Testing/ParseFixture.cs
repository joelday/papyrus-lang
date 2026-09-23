using Papyrus.CodeAnalysis;

namespace Papyrus.CodeAnalysis.Testing;

public sealed class ParseFixture
{
    public ParseFixture(string name, string source, PapyrusDialect dialect)
    {
        Name = name;
        Source = source;
        Dialect = dialect;
    }

    public string Name { get; }

    public string Source { get; }

    public PapyrusDialect Dialect { get; }
}
