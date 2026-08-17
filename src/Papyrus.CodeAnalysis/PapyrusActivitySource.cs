using System.Collections.Generic;
using System.Diagnostics;

namespace Papyrus.CodeAnalysis;

public static class PapyrusActivitySource
{
    public const string Name = "Papyrus.CodeAnalysis";

    public static ActivitySource Source { get; } = new(Name);

    public static Activity? Start(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        return Source.StartActivity(name, ActivityKind.Internal, default(ActivityContext), tags);
    }
}
