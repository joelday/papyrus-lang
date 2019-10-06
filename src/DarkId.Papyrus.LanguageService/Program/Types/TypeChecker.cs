using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DarkId.Papyrus.Common;

using DarkId.Papyrus.LanguageService.Program.Symbols;
using DarkId.Papyrus.LanguageService.Syntax;

namespace DarkId.Papyrus.LanguageService.Program.Types
{
    public class TypeChecker
    {
        private static string CreateArrayTypeSource(ObjectIdentifier objectIdentifier)
        {
            var elementTypeName = objectIdentifier.ShortName;
            var namespacePrefix = objectIdentifier.NamespaceParts.Length > 0 ? objectIdentifier.NamespaceParts.Join(":") + ":" : string.Empty;

            throw new NotImplementedException();
#if FALLOUT4
            return $@"Scriptname {namespacePrefix}Array___{elementTypeName.Replace(":", "_")} native

Function Clear() native
{{Removes all items from the array, reducing it to a 0-length array.}}

Function Add({elementTypeName} akElement, int aiCount = 1) native
{{Adds a new item to the array at the end, expanding it to fit. May add multiple items at once.}}

Function Insert({elementTypeName} akElement, int aiLocation) native
{{Inserts a new item to the array at the specified position, expanding it to fit.}}

Function Remove(int aiLocation, int aiCount = 1) native
{{Removes an item or group of items from the array at the specified position, shrinking it to fit.}}

Function RemoveLast() native
{{Removes the last item from the array, shrinking it to fit.}}

int Function Find({elementTypeName} akElement, int aiStartIndex = 0) native
{{Locates a particular value inside an array and returns the index.}}

int Function RFind({elementTypeName} akElement, int aiStartIndex = -1) native
{{Locates a particular value inside an array and returns the index, starting from the end of the array, and moving to the beginning.}}

{(string.IsNullOrEmpty(objectIdentifier.StructName) ? string.Empty : $@"
int Function FindStruct(string asVarName, var akElement, int aiStartIndex = 0) native
{{Locates a particular value in a struct inside an array and returns the index}}

int Function RFindStruct(string asVarName, var akElement, int aiStartIndex = -1) native
{{Locates a particular value in a struct inside an array and returns the index, starting from the end of the array, and moving to the beginning.}}
")}

int Property Length_ const auto
{{The length of the array.}}
";
#elif SKYRIM
            return $@"Scriptname {namespacePrefix}Array___{elementTypeName.Replace(":", "_")} native

int Function Find({elementTypeName} akElement, int aiStartIndex = 0) native
{{Locates a particular value inside an array and returns the index.}}

int Function RFind({elementTypeName} akElement, int aiStartIndex = -1) native
{{Locates a particular value inside an array and returns the index, starting from the end of the array, and moving to the beginning.}}

int Property Length_ auto
{{The length of the array.}}
";
#endif
        }

        private readonly PapyrusProgram _program;
        // private readonly CompilerTypeTable _compilerTypeTable;
        private readonly TypeEvaluationVisitor _typeEvaluationVisitor;
        private readonly Dictionary<ObjectIdentifier, ArrayType> _arrayTypes =
            new Dictionary<ObjectIdentifier, ArrayType>();

        internal TypeEvaluationVisitor TypeEvaluationVisitor => _typeEvaluationVisitor;

        public TypeChecker(PapyrusProgram program)
        {
            _program = program;
            // _compilerTypeTable = new CompilerTypeTable();
            _typeEvaluationVisitor = new TypeEvaluationVisitor(this);
        }

        private ArrayType GetArrayType(PapyrusType elementType)
        {
            //var objectIdentifier = elementType.Name;

            //lock (_arrayTypes)
            //{
            //    if (!_arrayTypes.ContainsKey(objectIdentifier))
            //    {
            //        var source = CreateArrayTypeSource(objectIdentifier);
            //        var stream = new CaseInsensitiveStringStream(source);
            //        var lexer = new PapyrusLexer(stream);
            //        var parser = new PapyrusParser(new CommonTokenStream(lexer));
            //        parser.AsDynamic().KnownUserFlags = _program.FlagsFile.NativeFlagsDict;
            //        parser.script();

            //        var compilerType = parser.ParsedObject;

            //        var nodeBinder = new NodeBinder();

            //        var node = nodeBinder.Bind(null, _program,
            //            new ScriptText(null, source, "0"), compilerType.GetTokenStream(), compilerType.GetAst());

            //        var scopeBinder = new ScopeBinder();
            //        var scopeResult = scopeBinder.Bind(compilerType, node.Value);

            //        node.Diagnostics.AddRange(scopeResult.Diagnostics);
                    
            //        var symbolBinder = new SymbolBinder();
            //        var symbolResult = symbolBinder.Bind(node.Value);

            //        var type = new ArrayType(_program, symbolResult.Value, compilerType, elementType.Name);
            //        _arrayTypes.Add(objectIdentifier, type);
            //    }

            //    return _arrayTypes[objectIdentifier];
            //}

            throw new NotImplementedException();
        }

        public PapyrusType GetTypeForObjectId(ObjectIdentifier objectIdentifier, bool asArray)
        {
            var type = GetTypeForObjectId(objectIdentifier);
            if (asArray)
            {
                return GetArrayType(type as PapyrusType ?? new IntrinsicType(_program, objectIdentifier));
            }

            return type;
        }

        public ComplexType GetTypeForObjectId(ObjectIdentifier objectIdentifier)
        {
            var scriptIdentifier = ObjectIdentifier.Parse(objectIdentifier.FullScriptName);
            _program.ScriptFiles.TryGetValue(scriptIdentifier, out var scriptFile);
            var scriptType = scriptFile?.Type;

            if (scriptType == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(objectIdentifier.StructName))
            {
                scriptType.StructTypes.TryGetValue(objectIdentifier, out var structType);
                return structType;
            }

            return scriptType;
        }
    }
}
