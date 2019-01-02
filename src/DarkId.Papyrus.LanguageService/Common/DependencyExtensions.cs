using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DarkId.Papyrus.LanguageService.Common
{
    public static class DependencyExtensions
    {
        public static T CreateInstance<T>(this IServiceProvider serviceProvider, params object[] parameters)
        {
            return ActivatorUtilities.CreateInstance<T>(serviceProvider, parameters);
        }
    }
}