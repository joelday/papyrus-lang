using System;
using System.Collections.Generic;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using PapCommon = DarkId.Papyrus.LanguageService.Common;
using PapProgram = DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.Server
{
    public static class ModelExtensions
    {
        private static readonly Dictionary<PapProgram.DiagnosticLevel, DiagnosticSeverity> _severityMap =
            new Dictionary<PapProgram.DiagnosticLevel, DiagnosticSeverity>()
            {
               { PapProgram.DiagnosticLevel.Error, DiagnosticSeverity.Error }
            };

        public static PapCommon.Position ToPosition(this Position position)
        {
            return new PapCommon.Position()
            {
                Line = position.Line,
                Character = position.Character
            };
        }

        public static Position ToPosition(this PapCommon.Position position)
        {
            return new Position()
            {
                Line = position.Line,
                Character = position.Character
            };
        }

        public static PapCommon.Range ToRange(this Range range)
        {
            return new PapCommon.Range()
            {
                Start = range.Start.ToPosition(),
                End = range.End.ToPosition()
            };
        }

        public static Range ToRange(this PapCommon.Range range)
        {
            return new Range()
            {
                Start = range.Start.ToPosition(),
                End = range.End.ToPosition()
            };
        }

        public static PapCommon.ScriptTextChange ToScriptTextChange(this TextDocumentContentChangeEvent changeEvent)
        {
            return new PapCommon.ScriptTextChange()
            {
                Range = changeEvent.Range.ToRange(),
                RangeLength = changeEvent.RangeLength,
                Text = changeEvent.Text
            };
        }

        public static Diagnostic ToDiagnostic(this PapProgram.Diagnostic diagnostic, string prefix = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix))
            {
                sb.Append($"[{prefix}]: ");
            }

            sb.Append(diagnostic.Message);

            if (diagnostic.Exception != null)
            {
                sb.Append($"\r\n({diagnostic.Exception.ToString()})");
            }

            return new Diagnostic()
            {
                Message = sb.ToString(),
                Severity = _severityMap[diagnostic.Severity],
                Range = diagnostic.Range.ToRange(),
                Code = new DiagnosticCode(null),
                Source = "Papyrus"
            };
        }
    }
}