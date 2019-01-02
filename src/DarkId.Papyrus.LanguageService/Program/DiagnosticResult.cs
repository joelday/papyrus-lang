using DarkId.Papyrus.LanguageService.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.LanguageService.Program
{
    class DiagnosticResult<T>
    {
        public static DiagnosticResult<T> TryWithDiagnostics(Func<List<Diagnostic>, T> valueFunc, List<Diagnostic> diagnostics = null)
        {
            var result = new DiagnosticResult<T>
            {
                Diagnostics = diagnostics ?? new List<Diagnostic>()
            };

            try
            {
                result.Value = valueFunc(result.Diagnostics);
            }
            catch (Exception e)
            {
                result.Diagnostics.Add(e.ToDiagnostic());
            }

            return result;
        }

        public List<Diagnostic> Diagnostics { get; private set; }
        public T Value { get; private set; }
    }

    class DiagnosticResult
    {
        public static DiagnosticResult TryWithDiagnostics(Action<List<Diagnostic>> action, List<Diagnostic> diagnostics = null)
        {
            var result = new DiagnosticResult
            {
                Diagnostics = diagnostics ?? new List<Diagnostic>()
            };

            try
            {
                action(result.Diagnostics);
            }
            catch (Exception e)
            {
                result.Diagnostics.Add(e.ToDiagnostic());
            }

            return result;
        }

        public List<Diagnostic> Diagnostics { get; private set; }
    }
}
