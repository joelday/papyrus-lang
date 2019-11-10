//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using DarkId.Papyrus.LanguageService.Program.Symbols;
//using DarkId.Papyrus.LanguageService.Syntax;
//using DarkId.Papyrus.LanguageService.Syntax.Legacy;

//namespace DarkId.Papyrus.LanguageService.Program.Types
//{
//    class TypeEvaluationVisitor : SyntaxNodeVisitor<PapyrusType>
//    {
//        private readonly TypeChecker _typeChecker;

//        public TypeEvaluationVisitor(TypeChecker typeChecker)
//        {
//            _typeChecker = typeChecker;
//        }

//        public override PapyrusType VisitIdentifierExpression(IdentifierExpressionNode node)
//        {
//            return node.Identifier.GetDeclaredOrReferencedSymbol()?.GetPapyrusType();
//        }

//        public override PapyrusType VisitArrayIndexExpression(ArrayIndexExpressionNode node)
//        {
//            if (!(Visit(node.ArrayExpression) is ArrayType arrayType))
//            {
//                return null;
//            }

//            return _typeChecker.GetTypeForObjectId(arrayType.ElementType);
//        }

//        public override PapyrusType VisitCastExpression(CastExpressionNode node)
//        {
//            return node.TypeIdentifier.GetReferencedType();
//        }

//        public override PapyrusType VisitFunctionCallExpression(FunctionCallExpressionNode node)
//        {
//            return node.Identifier.GetDeclaredOrReferencedSymbol()?.GetPapyrusType();
//        }

//        public override PapyrusType VisitMemberAccessExpression(MemberAccessExpressionNode node)
//        {
//            if (node == null)
//            {
//                return null;
//            }

//            return Visit(node.AccessExpression);
//        }

//        public override PapyrusType VisitBinaryOperationExpression(BinaryOperationExpressionNode node)
//        {
//            // TODO: boolean
//            return base.VisitBinaryOperationExpression(node);
//        }

//        public override PapyrusType VisitIsExpression(IsExpressionNode node)
//        {
//            // TODO: boolean
//            return base.VisitIsExpression(node);
//        }

//        public override PapyrusType VisitNewArrayExpression(NewArrayExpressionNode node)
//        {
//            return node.TypeIdentifier.GetReferencedType();
//        }

//        public override PapyrusType VisitLiteralExpression(LiteralExpressionNode node)
//        {
//            // TODO: intrinsics
//            return base.VisitLiteralExpression(node);
//        }

//        public override PapyrusType VisitNewStructExpression(NewStructExpressionNode node)
//        {
//            return node.TypeIdentifier.GetReferencedType();
//        }

//        public override PapyrusType VisitUnaryOperationExpression(UnaryOperationExpressionNode node)
//        {
//            // TODO: boolean
//            return base.VisitUnaryOperationExpression(node);
//        }
//    }
//}
