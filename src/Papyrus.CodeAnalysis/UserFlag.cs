using System;

namespace Papyrus.CodeAnalysis;

/// <summary>
/// Where a user flag may be applied, from the <c>{ ... }</c> list after its bit index.
/// A flag applied anywhere else is ignored on emit, as Caprica and the CK compiler do.
/// </summary>
[Flags]
public enum UserFlagLocations
{
    None = 0,
    Script = 1 << 0,
    Property = 1 << 1,
    Variable = 1 << 2,
    StructVar = 1 << 3,
    Function = 1 << 4,
    Group = 1 << 5,
    All = Script | Property | Variable | StructVar | Function | Group,
}

public sealed class UserFlag
{
    public UserFlag(string name, int bitIndex, UserFlagLocations locations = UserFlagLocations.All)
    {
        Name = name;
        BitIndex = bitIndex;
        Locations = locations;
        Mask = bitIndex is >= 0 and <= 31 ? 1u << bitIndex : 0;
    }

    private UserFlag(string name, uint mask, UserFlagLocations locations)
    {
        Name = name;
        BitIndex = -1;
        Locations = locations;
        Mask = mask;
    }

    /// <summary>
    /// A flag defined as <c>Flag Name Child &amp; Child ...</c>: applying it applies its
    /// children's bits. It has no bit of its own and is not written to the PEX flag table.
    /// </summary>
    public static UserFlag Composite(string name, uint mask, UserFlagLocations locations)
        => new(name, mask, locations);

    public string Name { get; }

    /// <summary>The flag's own bit, or -1 for a composite flag.</summary>
    public int BitIndex { get; }

    public bool IsComposite => BitIndex < 0;

    public UserFlagLocations Locations { get; }

    /// <summary>The bits this flag sets when applied (0 when its bit index is out of range).</summary>
    public uint Mask { get; }
}
