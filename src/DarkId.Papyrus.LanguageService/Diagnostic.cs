using DarkId.Papyrus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService
{
    public class Diagnostic
    {
        public DiagnosticLevel Severity { get; }
        public string Message { get; }
        public TextRange Range { get; }
        public Exception Exception { get; }

        public Diagnostic(DiagnosticLevel severity, string message, TextRange range = default, Exception exception = null)
        {
            Severity = severity;
            Message = message;
            Range = range;
            Exception = exception;
        }
    }
}
