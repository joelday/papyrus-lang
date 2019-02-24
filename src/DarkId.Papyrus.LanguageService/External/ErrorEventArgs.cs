using ReflectionMagic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.External
{
    class ErrorEventArgs : EventArgs
    {
        private readonly object _internalErrorEventArgs;

        public ErrorEventArgs(object internalErrorEventArgs)
        {
            _internalErrorEventArgs = internalErrorEventArgs.AsDynamic();
        }

        public string ErrorText => _internalErrorEventArgs.AsDynamic().ErrorText;
        public int LineNumber => _internalErrorEventArgs.AsDynamic().LineNumber;
        public int ColumnNumber => _internalErrorEventArgs.AsDynamic().ColumnNumber;
    }
}