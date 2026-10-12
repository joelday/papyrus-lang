using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis;

public abstract class ScriptResolver
{
    public abstract bool TryResolve(QualifiedName name, out SourceText text, out string displayPath);

    public abstract bool TryResolvePex(string colonName, out PexScriptMetadata metadata);
}
