using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Syntax
{
    public class TokenNode : SyntaxNode
    {
        internal TokenNode(Internal.TokenNode node, SyntaxNode parent, int position)
            : base(node, parent, position)
        {
        }
    }
}
