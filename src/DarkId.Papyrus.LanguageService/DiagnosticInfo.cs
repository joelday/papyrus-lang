using DarkId.Papyrus.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkId.Papyrus.LanguageService
{
    public class DiagnosticInfo
    {
        public DiagnosticLevel Severity { get; }
        public int Id { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public DiagnosticInfo(DiagnosticLevel severity, int id, string message, Exception exception = null)
        {
            Severity = severity;
            Id = id;
            Message = message;
            Exception = exception;
        }
    }
}
