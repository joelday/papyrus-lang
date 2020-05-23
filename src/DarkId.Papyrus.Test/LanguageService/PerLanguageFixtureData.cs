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
        private TestFixtureData CreateFixtureData(LanguageVersion languageVersion)
        {
            var fixtureData = new TestFixtureData();
            fixtureData.Properties.Add("LanguageVersion", LanguageVersion.Fallout4);
            return fixtureData;
        }

        public IEnumerator GetEnumerator()
        {
            yield return CreateFixtureData(LanguageVersion.Fallout4);
            yield return CreateFixtureData(LanguageVersion.Skyrim);
        }
    }
}
