using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public enum BinaryOperatorType
    {
        None,
        Add,
        BooleanAnd,
        BooleanOr,
        CompareEqual,
        CompareGreaterThan,
        CompareGreaterThanOrEqual,
        CompareLessThan,
        CompareLessThanOrEqual,
        CompareNotEqual,
        Divide,
        Modulus,
        Multiply,
        Subtract,
    }
}
