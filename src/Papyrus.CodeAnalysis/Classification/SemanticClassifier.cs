using System.Collections.Immutable;
using System.Threading;
using Papyrus.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Classification;

/// <summary>
/// Classifies like <see cref="SyntacticClassifier"/>, and additionally every name by the
/// symbol it resolves to (a call, a property or variable reference, a script or struct
/// type), with Const/AutoReadOnly, Global, and doc-comment modifiers.
/// </summary>
public static class SemanticClassifier
{
    public static ImmutableArray<ClassifiedSpan> Classify(
        SemanticModel model,
        TextSpan? span = null,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(model);
        return SyntacticClassifier.ClassifyCore(model.SyntaxTree, model, span, cancellationToken);
    }
}
