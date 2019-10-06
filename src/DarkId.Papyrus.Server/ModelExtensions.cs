using System;
using System.Collections.Generic;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using PapCommon = DarkId.Papyrus.Common;
using LS = DarkId.Papyrus.LanguageService;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DarkId.Papyrus.Server
{
    public static class ModelExtensions
    {
        private static readonly Dictionary<LS.DiagnosticLevel, DiagnosticSeverity> SeverityMap =
            new Dictionary<LS.DiagnosticLevel, DiagnosticSeverity>()
            {
               { LS.DiagnosticLevel.Error, DiagnosticSeverity.Error }
            };

        public static PapCommon.TextPosition ToPosition(this Position position)
        {
            return new PapCommon.TextPosition(position.Line, position.Character);
        }

        public static Position ToPosition(this PapCommon.TextPosition position)
        {
            return new Position()
            {
                Line = position.Line,
                Character = position.Character
            };
        }

        public static PapCommon.TextRange ToRange(this Range range)
        {
            return new PapCommon.TextRange(range.Start.ToPosition(), range.End.ToPosition());
        }

        public static Range ToRange(this PapCommon.TextRange range)
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

        public static Diagnostic ToDiagnostic(this LS.Diagnostic diagnostic, string prefix = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix))
            {
                sb.Append($"[{prefix}]: ");
            }

            sb.Append(diagnostic.Message);

            if (diagnostic.Exception != null)
            {
                sb.Append($"\r\n({diagnostic.Exception})");
            }

            return new Diagnostic()
            {
                Message = sb.ToString(),
                Severity = SeverityMap[diagnostic.Severity],
                Range = diagnostic.Range.ToRange(),
                Code = new DiagnosticCode(null),
                Source = "Papyrus"
            };
        }
    }
}