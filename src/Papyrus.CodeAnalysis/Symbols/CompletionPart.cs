using System;

namespace Papyrus.CodeAnalysis.Symbols;

[Flags]
internal enum CompletionPart
{
    None = 0,
    Members = 1 << 0,
    BaseType = 1 << 1,
    MemberSignatures = 1 << 2,
    MemberBodies = 1 << 3,
    All = Members | BaseType | MemberSignatures | MemberBodies,
}
