using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public struct ScriptTextChange
    {
        public Range Range { get; set; }
        public int RangeLength { get; set; }
        public string Text { get; set; }
    }
}