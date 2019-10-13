using DarkId.Papyrus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService
{
    public class DiagnosticInfo
    {
        public DiagnosticLevel Severity { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public DiagnosticInfo(DiagnosticLevel severity, string message, Exception exception = null)
        {
            Severity = severity;
            Message = message;
            Exception = exception;
        }
    }
}
