using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Program.Symbols;
using PCompiler;

#if SKYRIM
using ScriptComplexType = PCompiler.ScriptObjectType;
#endif

namespace DarkId.Papyrus.LanguageService.Program.Types
{
    public abstract class PapyrusType
    {
        private readonly PapyrusProgram _program;
        protected PapyrusProgram Program => _program;

        private readonly ObjectIdentifier _name;
        public virtual ObjectIdentifier Name => _name;

        internal PapyrusType(PapyrusProgram program, ObjectIdentifier name)
        {
            _program = program;
            _name = name;
        }
    }

    public abstract class ComplexType : PapyrusType
    {
        private readonly TypeSymbol _symbol;
        public TypeSymbol Symbol => _symbol;

        private readonly ScriptComplexType _compilerType;
        protected ScriptComplexType CompilerType => _compilerType;

        internal ComplexType(PapyrusProgram program, TypeSymbol symbol, ScriptComplexType compilerType) :
            base(program, symbol.Id)
        {
            _symbol = symbol;
            _compilerType = compilerType;
        }
    }

    public sealed class IntrinsicType : PapyrusType
    {
        public IntrinsicType(PapyrusProgram program, ObjectIdentifier name) : base(program, name)
        {
        }
    }

    public abstract class ComplexType<TSymbol, TCompilerType> : ComplexType
        where TSymbol : TypeSymbol
        where TCompilerType : ScriptComplexType
    {
        protected new TSymbol Symbol => base.Symbol as TSymbol;
        protected new TCompilerType CompilerType => base.CompilerType as TCompilerType;

        internal ComplexType(PapyrusProgram program, TSymbol symbol, TCompilerType compilerType) :
            base(program, symbol, compilerType)
        {
        }
    }

    public sealed class ScriptType : ComplexType<ScriptSymbol, ScriptObjectType>
    {
#if FALLOUT4
        private readonly Dictionary<ObjectIdentifier, StructType> _structTypes;
        public IReadOnlyDictionary<ObjectIdentifier, StructType> StructTypes => _structTypes;
#endif
        public ScriptType(PapyrusProgram program, ScriptSymbol symbol, ScriptObjectType compilerType)
            : base(program, symbol, compilerType)
        {
#if FALLOUT4
            _structTypes = symbol.Children.OfType<StructSymbol>().Select(s =>
            {
                CompilerType.TryGetStruct(s.Name, out var structType);

                return new StructType(program, s, structType);
            }).ToDictionary(s => s.Name);
#endif
        }
    }

    public sealed class ArrayType : ComplexType<ScriptSymbol, ScriptObjectType>
    {
        private readonly ObjectIdentifier _elementType;
        public ObjectIdentifier ElementType => _elementType;

        public override ObjectIdentifier Name => ObjectIdentifier.Parse(_elementType.FullyQualifiedDisplayName + "[]");

        public ArrayType(PapyrusProgram program, ScriptSymbol symbol, ScriptObjectType compilerType, ObjectIdentifier elementType)
            : base(program, symbol, compilerType)
        {
            Symbol.SyntheticArrayType = this;
            _elementType = elementType;
        }
    }

#if FALLOUT4
    public sealed class StructType : ComplexType<StructSymbol, ScriptStructType>
    {
        public StructType(PapyrusProgram program, StructSymbol symbol, ScriptStructType compilerType)
            : base(program, symbol, compilerType)
        {
        }
    }
#endif

}
