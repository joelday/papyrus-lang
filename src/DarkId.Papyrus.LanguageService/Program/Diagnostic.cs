using DarkId.Papyrus.LanguageService.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class Diagnostic
    {
        public DiagnosticLevel Severity { get; }
        public string Message { get; }
        public virtual Range Range { get; }
        public Exception Exception { get; }

        public Diagnostic(DiagnosticLevel severity, string message, Range range, Exception exception = null)
        {
            Severity = severity;
            Message = message;
            Range = range;
            Exception = exception;
        }
    }
}
