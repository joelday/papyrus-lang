using System;
using System.Collections.Generic;

namespace Papyrus.CodeAnalysis;

public sealed class NameTable
{
    private readonly Dictionary<string, PapyrusName> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PapyrusName>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public NameTable()
    {
        _lookup = _names.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public PapyrusName Get(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Get(text.AsSpan());
    }

    public PapyrusName Get(ReadOnlySpan<char> text)
    {
        lock (_names)
        {
            if (_lookup.TryGetValue(text, out var name))
            {
                return name;
            }

            name = new PapyrusName(text.ToString());
            _names.Add(name.Text, name);
            return name;
        }
    }
}
