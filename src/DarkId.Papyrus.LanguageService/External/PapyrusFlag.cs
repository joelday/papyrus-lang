using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReflectionMagic;

namespace DarkId.Papyrus.LanguageService.External
{
    class PapyrusFlag : IPapyrusFlag
    {
        private readonly dynamic _flag;

        public PapyrusFlag(object flag)
        {
            _flag = flag.AsDynamic();
        }

        public uint Value => _flag.Value;

        public bool AllowedOnObj => _flag.AllowedOnObj;

        public bool AllowedOnProp => _flag.AllowedOnProp;

        public bool AllowedOnVar => _flag.AllowedOnVar;

        public bool AllowedOnStructVar => _flag.AllowedOnStructVar;

        public bool AllowedOnFunc => _flag.AllowedOnFunc;

        public bool AllowedOnGroup => _flag.AllowedOnGroup;
    }
}