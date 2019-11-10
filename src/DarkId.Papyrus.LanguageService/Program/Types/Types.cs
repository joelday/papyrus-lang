//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using DarkId.Papyrus.LanguageService.Program.Symbols;

//namespace DarkId.Papyrus.LanguageService.Program.Types
//{
//    public abstract class PapyrusType
//    {
//        protected PapyrusProgram Program { get; }
//        public virtual ObjectIdentifier Name { get; }

//        internal PapyrusType(PapyrusProgram program, ObjectIdentifier name)
//        {
//            Program = program;
//            Name = name;
//        }
//    }

//    public abstract class ComplexType : PapyrusType
//    {
//        public TypeSymbol Symbol { get; }

//        internal ComplexType(PapyrusProgram program, TypeSymbol symbol) :
//            base(program, symbol.Id)
//        {
//            Symbol = symbol;
//        }
//    }

//    public sealed class IntrinsicType : PapyrusType
//    {
//        public IntrinsicType(PapyrusProgram program, ObjectIdentifier name) : base(program, name)
//        {
//        }
//    }

//    public abstract class ComplexType<TSymbol> : ComplexType
//        where TSymbol : TypeSymbol
//    {
//        public new TSymbol Symbol => base.Symbol as TSymbol;

//        internal ComplexType(PapyrusProgram program, TSymbol symbol) :
//            base(program, symbol)
//        {
//        }
//    }

//    public sealed class ScriptType : ComplexType<ScriptSymbol>
//    {
//        // private readonly Dictionary<ObjectIdentifier, StructType> _structTypes;
//        public IReadOnlyDictionary<ObjectIdentifier, StructType> StructTypes => throw new NotImplementedException(); //_structTypes;

//        public ScriptType(PapyrusProgram program, ScriptSymbol symbol)
//            : base(program, symbol)
//        {
//            //_structTypes = symbol.Children.OfType<StructSymbol>().Select(s =>
//            //{
//            //    CompilerType.TryGetStruct(s.Name, out var structType);

//            //    return new StructType(program, s, structType);
//            //}).ToDictionary(s => s.Name);
//        }
//    }

//    public sealed class ArrayType : ComplexType<ScriptSymbol>
//    {
//        private readonly ObjectIdentifier _elementType;
//        public ObjectIdentifier ElementType => _elementType;

//        public override ObjectIdentifier Name => ObjectIdentifier.Parse(_elementType.FullyQualifiedDisplayName + "[]");

//        public ArrayType(PapyrusProgram program, ScriptSymbol symbol, ObjectIdentifier elementType)
//            : base(program, symbol)
//        {
//            Symbol.SyntheticArrayType = this;
//            _elementType = elementType;
//        }
//    }

//    public sealed class StructType : ComplexType<StructSymbol>
//    {
//        public StructType(PapyrusProgram program, StructSymbol symbol)
//            : base(program, symbol)
//        {
//        }
//    }

//}
