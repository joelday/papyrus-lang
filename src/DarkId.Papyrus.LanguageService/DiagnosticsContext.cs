using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkId.Papyrus.LanguageService
{
    public class DiagnosticsContext
    {
        private readonly ConcurrentBag<Diagnostic> _diagnostics = new ConcurrentBag<Diagnostic>();
        public IEnumerable<Diagnostic> Diagnostics => _diagnostics.OrderBy(d => d.Range.Start);

        internal void Add(Diagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }
    }
}
