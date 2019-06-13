using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program.Syntax;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    public class DisplayText
    {
        public string Kind { get; set; }
        public string Text { get; set; }
        public string Documentation { get; set; }
    }

    public class FunctionDisplayText
    {
        public string Kind { get; set; }
        public string Prefix { get; set; }
        public string ShortNamePrefix { get; set; }
        public List<DisplayText> Parameters { get; set; }
        public string Postfix { get; set; }
        public string Documentation { get; set; }
    }

    public class DisplayTextEmitter
    {
        public string GetEventHandlerSignatureText(PapyrusSymbol symbol)
        {
            if (symbol is EventSymbol eventSymbol)
            {
                var displayText = GetDisplayTextForEvent(eventSymbol);
                return $"{eventSymbol.Name}({symbol.Parent.Name} akSender, {displayText.Parameters.Select(t => t.Text).Join(", ")})";
            }

#if FALLOUT4
            if (symbol is CustomEventSymbol customEventSymbol)
            {
                return $"{customEventSymbol.Name}({symbol.Parent.Name} akSender, Var[] akArgs)";
            }
#endif
            return null;
        }

        public DisplayText GetDisplayText(PapyrusSymbol symbol)
        {
            if (symbol is AliasedSymbol asAliased)
            {
                return GetDisplayText(asAliased.Aliased);
            }

            // TODO: User flags

            switch (symbol.Kind)
            {
                case SymbolKinds.CustomEvent:
                    return GetBasicDisplayText(symbol.Name, "custom event", "CustomEvent");
                case SymbolKinds.Event:
                    var eventDisplayText = GetDisplayTextForEvent((EventSymbol)symbol);
                    return new DisplayText()
                    {
                        Kind = eventDisplayText.Kind,
                        Text = $"{eventDisplayText.Prefix}({eventDisplayText.Parameters.Select(t => t.Text).Join(", ")}){eventDisplayText.Postfix}",
                        Documentation = eventDisplayText.Documentation
                    };
                case SymbolKinds.Function:
                    var funcDisplayText = GetDisplayTextForFunction((FunctionSymbol)symbol);
                    return new DisplayText()
                    {
                        Kind = funcDisplayText.Kind,
                        Text = $"{funcDisplayText.Prefix}({funcDisplayText.Parameters.Select(t => t.Text).Join(", ")}){funcDisplayText.Postfix}",
                        Documentation = funcDisplayText.Documentation
                    };
                case SymbolKinds.Group:
                    return GetBasicDisplayText(symbol.Name, "group", "Group");
                case SymbolKinds.Import:
                    return GetBasicDisplayText(symbol.Name, "import", "Import");
                case SymbolKinds.Property:
                    return GetDisplayTextForProperty((PropertySymbol)symbol);
                case SymbolKinds.State:
                    return GetDisplayTextForState((StateSymbol)symbol);
                case SymbolKinds.Struct:
                    return GetBasicDisplayText(
                        symbol.GetPapyrusType()?.Name.FullyQualifiedDisplayName
                        ?? symbol.Name, "struct", "Struct");
                case SymbolKinds.Variable:
                    return GetDisplayTextForVariable((VariableSymbol)symbol);
                case SymbolKinds.Script:
                    return GetDisplayTextForScript((ScriptSymbol)symbol);
            }

            return null;
        }

        private DisplayText GetBasicDisplayText(string name, string kind, string keyword)
        {
            var sb = new StringBuilder();
            sb.Append($"{keyword} {name}");

            return new DisplayText()
            {
                Kind = kind,
                Text = sb.ToString()
            };
        }

        private DisplayText GetDisplayTextForState(StateSymbol symbol)
        {
            var sb = new StringBuilder();

            if ((symbol.Flags & LanguageFlags.Auto) != 0)
            {
                sb.Append("Auto ");
            }

            sb.Append(symbol.Name);

            return new DisplayText()
            {
                Kind = "state",
                Text = sb.ToString(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }

        private DisplayText GetDisplayTextForScript(ScriptSymbol symbol)
        {
            var sb = new StringBuilder();

            var scriptType = symbol.GetPapyrusType();
            sb.Append($"Scriptname {scriptType?.Name.FullyQualifiedDisplayName ?? symbol.Name}");

            if (symbol.ExtendedScript != null)
            {
                var extendedType = symbol.ExtendedScript.GetPapyrusType();
                sb.Append(" extends " + extendedType?.Name.FullyQualifiedDisplayName ?? symbol.ExtendedScript.Name);
            }

            if (symbol.Flags != LanguageFlags.None)
            {
                sb.Append(" ");
                sb.Append(symbol.Flags.ToString().Replace(",", string.Empty));
            }

            return new DisplayText()
            {
                Kind = "script",
                Text = sb.ToString(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }

        private DisplayText GetDisplayTextForVariable(VariableSymbol symbol)
        {
            var sb = new StringBuilder();

            var valueType = symbol.GetPapyrusType();
            sb.Append(valueType != null ? valueType.Name.FullyQualifiedDisplayName.ToString() : symbol.Definition.TypeIdentifier.Text);
            sb.Append(" ");

            if (symbol.Parent is TypeSymbol typeSymbol)
            {
                sb.Append(typeSymbol.GetPapyrusType()?.Name.ShortName ?? typeSymbol.Name);
                sb.Append(".");
            }

            sb.Append(symbol.Name);

            // TODO: Default value

            return new DisplayText()
            {
                Kind = ((SyntaxNode)symbol.Definition).Parent is FunctionHeaderNode ? "parameter" : "variable",
                Text = sb.ToString(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }

        private DisplayText GetDisplayTextForProperty(PropertySymbol symbol)
        {
            var sb = new StringBuilder();

            var valueType = symbol.GetPapyrusType();
            sb.Append(valueType != null ? valueType.Name.FullyQualifiedDisplayName : symbol.Definition.Header.TypeIdentifier.Text);
            sb.Append(" Property ");

            sb.Append($"{symbol.Script.Id.ShortName}.{symbol.Name}");

            if (symbol.Flags != LanguageFlags.None)
            {
                sb.Append(" ");
                sb.Append(symbol.Flags.ToString().Replace(",", string.Empty));
            }

            return new DisplayText()
            {
                Kind = "property",
                Text = sb.ToString(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }

        public FunctionDisplayText GetDisplayTextForEvent(EventSymbol symbol)
        {
            return new FunctionDisplayText()
            {
                Kind = "event",
                Prefix = $"Event {symbol.Script.Id}.{symbol.Name}",
                ShortNamePrefix = $"Event {symbol.Script.Id.ScriptName}.{symbol.Name}",
                Parameters = symbol.Definition.Header.Parameters.Select(p =>
                    GetDisplayTextForVariable((VariableSymbol)p.Symbol)).ToList(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }

        public FunctionDisplayText GetDisplayTextForFunction(FunctionSymbol symbol)
        {
            var prefix = new StringBuilder();
            var shortNamePrefix = new StringBuilder();

            var typeIdentifier = symbol.Definition.Header.TypeIdentifier;
            if (typeIdentifier != null)
            {
                var returnType = symbol.GetPapyrusType();
                prefix.Append(returnType?.Name.FullyQualifiedDisplayName ?? typeIdentifier.Text + (typeIdentifier.IsArray ? "[]" : string.Empty));
                prefix.Append(" ");
            }

            shortNamePrefix.Append(prefix.ToString());

            prefix.Append($"Function {symbol.Script.Id}.{symbol.Name}");
            shortNamePrefix.Append($"Function {symbol.Script.Id.ScriptName}.{symbol.Name}");

            var postfix = new StringBuilder();

            if (symbol.Flags != LanguageFlags.None)
            {
                postfix.Append(" ");
                postfix.Append(symbol.Flags.ToString().Replace(",", string.Empty));
            }

            return new FunctionDisplayText()
            {
                Kind = "function",
                Prefix = prefix.ToString(),
                ShortNamePrefix = shortNamePrefix.ToString(),
                Parameters = symbol.Definition.Header.Parameters.Select(p =>
                    GetDisplayTextForVariable((VariableSymbol)p.Symbol)).ToList(),
                Postfix = postfix.ToString(),
                Documentation = symbol.GetDocumentationMarkdown()
            };
        }
    }
}
