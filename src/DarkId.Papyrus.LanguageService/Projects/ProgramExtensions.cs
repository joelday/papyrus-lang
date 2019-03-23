using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DarkId.Papyrus.Common;
using DarkId.Papyrus.LanguageService.Program;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public static class ProgramExtensions
    {
        public static ProgramOptionsBuilder WithProject(this ProgramOptionsBuilder builder, PapyrusProjectInfo projectInfo)
        {
            var projectFileDirectory = Path.GetDirectoryName(PathUtilities.Normalize(projectInfo.ProjectFile));

            builder.WithName(Path.GetFileNameWithoutExtension(projectInfo.ProjectFile))
                .WithFlagsFileName(projectInfo.Project.Flags)
                .WithSourceIncludes(projectInfo.Project.Imports.Reverse().Select(import => new SourceInclude()
                {
                    Path = Path.GetFullPath(Path.Combine(projectFileDirectory, PathUtilities.Normalize(import)))
                }));

            if (projectInfo.Project.Folders != null && projectInfo.Project.Folders.Length > 0)
            {
                var folder = projectInfo.Project.Folders[0];
                builder.WithSourceIncludes(new SourceInclude()
                {
                    Path = Path.GetFullPath(Path.Combine(projectFileDirectory, PathUtilities.Normalize(folder.Value))),
                    Recursive = !folder.NoRecurse
                });
            }
            else if (projectInfo.Project.Scripts != null && projectInfo.Project.Scripts.Length > 0)
            {
                var scriptsInclude = new SourceInclude()
                {
                    Path = projectFileDirectory
                };

                foreach (var script in projectInfo.Project.Scripts)
                {
                    var scriptWithExtension = Path.HasExtension(script) ? script : script + ".psc";
                    scriptsInclude.Scripts.Add(Path.GetFullPath(Path.Combine(projectFileDirectory, PathUtilities.Normalize(scriptWithExtension))));
                }

                builder.WithSourceIncludes(scriptsInclude);
            }

            return builder;
        }
    }
}