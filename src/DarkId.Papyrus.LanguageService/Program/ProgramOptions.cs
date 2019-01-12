using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkId.Papyrus.LanguageService.Program
{
    public class ProgramOptions : ICloneable
    {
        public string Name { get; set; }
        public string FlagsFileName { get; set; }
        public ProgramSources Sources { get; set; } = new ProgramSources();
        public LanguageVariant LanguageVariant { get; set; } = LanguageVariant.Fallout4;

        public ProgramOptions Clone()
        {
            return new ProgramOptions()
            {
                Name = Name,
                FlagsFileName = FlagsFileName,
                Sources = new ProgramSources()
                {
                    Includes = Sources.Includes.Select(include => new SourceInclude()
                    {
                        Path = include.Path,
                        Recursive = include.Recursive
                    }).ToList()
                },
                LanguageVariant = LanguageVariant
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }

    public class ProgramSources
    {
        public List<SourceInclude> Includes { get; set; } = new List<SourceInclude>();
    }

    public class SourceInclude
    {
        public string Path { get; set; }
        public bool Recursive { get; set; } = true;
    }
}