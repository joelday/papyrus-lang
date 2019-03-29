using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr.Runtime.Tree;
using DarkId.Papyrus.LanguageService.Program.Syntax;
using DarkId.Papyrus.LanguageService.Program.Types;
using PCompiler;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    public abstract class PapyrusSymbol
    {
        private readonly PapyrusSymbol _parent;
        private readonly List<PapyrusSymbol> _children = new List<PapyrusSymbol>();
        private readonly SyntaxNode _node;
        private readonly IdentifierNode _identifier;
        private readonly IEnumerable<string> _documentation;

        public abstract SymbolKinds Kind { get; }
        public virtual LanguageFlags Flags => LanguageFlags.None;

        public SyntaxNode Definition => _node;
        public IdentifierNode Identifier => _identifier;

        internal ScriptScope CompilerScope => _node.CompilerScope;

        public virtual ScriptSymbol Script => this as ScriptSymbol ?? _parent.Script;

        public PapyrusSymbol Parent => _parent;
        public IReadOnlyList<PapyrusSymbol> Children => _children;

        public ScriptFile File => _node.Script.File;

        public virtual string Name => _identifier.Text;
        public virtual bool Hidden => false;

        public virtual IEnumerable<string> Documentation => _documentation;

        public PapyrusSymbol(SyntaxNode node, IdentifierNode identifier, PapyrusSymbol parent)
        {
            _node = node;
            _documentation = new string[] { _node.GetLeadingComments() };

            _identifier = identifier;

            _parent = parent;
            if (_parent != null)
            {
                lock (_parent._children)
                {
                    _parent._children.Add(this);
                }
            }
        }
    }

    public abstract class TypeSymbol : PapyrusSymbol
    {
        private readonly ObjectIdentifier _id;
        public virtual ObjectIdentifier Id => _id;

        protected TypeSymbol(SyntaxNode node, IdentifierNode identifier, PapyrusSymbol parent, ObjectIdentifier id) : base(node, identifier, parent)
        {
            _id = id;
        }
    }

    public class ScriptSymbol : TypeSymbol
    {
        public ScriptSymbol(ScriptNode node) :
            base(node, node.Header.Identifier, null, ObjectIdentifier.Parse(node.Header.Identifier.Text))
        {
        }

        public ArrayType SyntheticArrayType { get; set; }
        public override ObjectIdentifier Id => SyntheticArrayType == null ? base.Id : SyntheticArrayType.Name;
        public override string Name => SyntheticArrayType == null ? base.Name : SyntheticArrayType.Name.FullyQualifiedDisplayName;

        public new ScriptNode Definition => (ScriptNode)base.Definition;
        public ScriptSymbol ExtendedScript => Definition.Header.TypeIdentifier?.GetReferencedTypeSymbol() as ScriptSymbol;

        public override SymbolKinds Kind => SymbolKinds.Script;
        public override LanguageFlags Flags => Definition.Header.Flags;

        public override IEnumerable<string> Documentation
        {
            get
            {
                if (Definition.Header.DocComment != null)
                {
                    return new string[] { Definition.Header.DocComment.Comment }.Concat(base.Documentation);
                }

                return base.Documentation;
            }
        }
    }

#if FALLOUT4
    public class CustomEventSymbol : PapyrusSymbol
    {
        public CustomEventSymbol(CustomEventDefinitionNode node, PapyrusSymbol parent) : base(node, node.Identifier, parent)
        {
        }

        public new CustomEventDefinitionNode Definition => (CustomEventDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.CustomEvent;
    }
#endif

    public class EventSymbol : PapyrusSymbol
    {
        public EventSymbol(EventDefinitionNode node, PapyrusSymbol parent) : base(node, node.Header.Identifier, parent)
        {
        }

        public new EventDefinitionNode Definition => (EventDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Event;
        public override LanguageFlags Flags => Definition.Header.Flags;
    }

    public class FunctionSymbol : PapyrusSymbol
    {
        public FunctionSymbol(FunctionDefinitionNode node, PapyrusSymbol parent) : base(node, node.Header.Identifier, parent)
        {
        }

        public new FunctionDefinitionNode Definition => (FunctionDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Function;
        public override LanguageFlags Flags => Definition.Header.Flags;

        public override IEnumerable<string> Documentation
        {
            get
            {
                if (Definition.Header.DocComment != null)
                {
                    return new string[] { Definition.Header.DocComment.Comment }.Concat(base.Documentation);
                }

                return base.Documentation;
            }
        }
    }

    public class GroupSymbol : PapyrusSymbol
    {
        public GroupSymbol(GroupDefinitionNode node, PapyrusSymbol parent) : base(node, node.Header.Identifier, parent)
        {
        }

        public new GroupDefinitionNode Definition => (GroupDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Group;
    }

    public class ImportSymbol : PapyrusSymbol
    {
        public ImportSymbol(ImportNode node, PapyrusSymbol parent) : base(node, node.Identifier, parent)
        {
        }

        public new ImportNode Definition => (ImportNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Import;
    }

    public class PropertySymbol : PapyrusSymbol
    {
        public PropertySymbol(PropertyDefinitionNode node, PapyrusSymbol parent) : base(node, node.Header.Identifier, parent)
        {
        }

        public new PropertyDefinitionNode Definition => (PropertyDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Property;
        public override LanguageFlags Flags => Definition.Header.Flags;

        public override string Name
        {
            get
            {
                // This is a hack to work around the inability to parse a property called Length for the synthetic array types.

                if (base.Name == "Length_" && Definition.Script.File == null)
                {
                    return "Length";
                }

                return base.Name;
            }
        }

        public override IEnumerable<string> Documentation
        {
            get
            {
                if (Definition.Header.DocComment != null)
                {
                    return new string[] { Definition.Header.DocComment.Comment }.Concat(base.Documentation);
                }

                return base.Documentation;
            }
        }
    }

    public class StateSymbol : PapyrusSymbol
    {
        public StateSymbol(StateDefinitionNode node, PapyrusSymbol parent) : base(node, node.Identifier, parent)
        {
        }

        public new StateDefinitionNode Definition => (StateDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.State;
        public override LanguageFlags Flags => Definition.IsAuto ? LanguageFlags.Auto : LanguageFlags.None;
    }

#if FALLOUT4
    public class StructSymbol : TypeSymbol
    {
        public StructSymbol(StructDefinitionNode node, PapyrusSymbol parent) :
            base(node, node.Header.Identifier, parent, ObjectIdentifier.Parse(parent.Name + "#" + node.Header.Identifier.Text))
        {
        }

        public new StructDefinitionNode Definition => (StructDefinitionNode)base.Definition;

        public override SymbolKinds Kind => SymbolKinds.Struct;
    }
#endif

    public class VariableSymbol : PapyrusSymbol
    {
        public VariableSymbol(ITypedIdentifiable node, PapyrusSymbol parent) : base((SyntaxNode)node, node.Identifier, parent)
        {
        }

        public new ITypedIdentifiable Definition => (ITypedIdentifiable)base.Definition;

        public bool IsFunctionParameter => Definition is FunctionParameterNode;

        public override SymbolKinds Kind => SymbolKinds.Variable;
        public override LanguageFlags Flags => (Definition is IFlaggable asFlaggable) ? asFlaggable.Flags : LanguageFlags.None;

        public override IEnumerable<string> Documentation
        {
            get
            {
                if (Definition is IDocumentable asDocumentable && asDocumentable.DocComment != null)
                {
                    return new string[] { asDocumentable.DocComment.Comment }.Concat(base.Documentation);
                }

                return base.Documentation;
            }
        }
    }

    public class AliasedSymbol : PapyrusSymbol
    {
        private readonly string _name;
        private readonly SymbolKinds _kind;
        private readonly PapyrusSymbol _aliasedSymbol;

        public AliasedSymbol(string name, SymbolKinds kind, PapyrusSymbol aliasedSymbol, PapyrusSymbol parent)
            : base(aliasedSymbol.Definition, aliasedSymbol.Identifier, parent)
        {
            _name = name;
            _kind = kind;
            _aliasedSymbol = aliasedSymbol;
        }

        public PapyrusSymbol Aliased => _aliasedSymbol;

        public override SymbolKinds Kind => _kind;
        public override bool Hidden => true;
        public override string Name => _name;
        public override ScriptSymbol Script => _aliasedSymbol.Script;
    }
}
