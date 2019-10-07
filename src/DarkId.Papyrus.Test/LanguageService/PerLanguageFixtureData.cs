using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DarkId.Papyrus.LanguageService;
using NUnit.Framework;

namespace DarkId.Papyrus.Test.LanguageService
{
    public class PerLanguageFixtureData : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new TestFixtureData(new TestServiceInstance(LanguageVersion.Skyrim)
                .CreateProgram()).SetArgDisplayNames(nameof(LanguageVersion.Skyrim));

            yield return new TestFixtureData(new TestServiceInstance(LanguageVersion.Fallout4)
                .CreateProgram()).SetArgDisplayNames(nameof(LanguageVersion.Fallout4));
        }
    }
}
