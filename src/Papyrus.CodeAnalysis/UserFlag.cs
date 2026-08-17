namespace Papyrus.CodeAnalysis;

public sealed class UserFlag
{
    public UserFlag(string name, int bitIndex)
    {
        Name = name;
        BitIndex = bitIndex;
    }

    public string Name { get; }

    public int BitIndex { get; }
}
