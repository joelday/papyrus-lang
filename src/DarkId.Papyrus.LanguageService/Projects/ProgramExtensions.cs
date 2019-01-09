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
            builder.WithName(Path.GetFileNameWithoutExtension(projectInfo.ProjectFile))
                .WithFlagsFileName(projectInfo.Project.Flags)
                .WithSourceIncludes(projectInfo.Project.Imports.Select(import => new SourceInclude()
                {
                    Path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PathUtilities.Normalize(projectInfo.ProjectFile)), PathUtilities.Normalize(import)))
                }));

            if (projectInfo.Project.Folders.Length > 0)
            {
                var folder = projectInfo.Project.Folders[0];
                builder.WithSourceIncludes(new SourceInclude()
                {
                    Path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(PathUtilities.Normalize(projectInfo.ProjectFile)), PathUtilities.Normalize(folder.Value))),
                    Recursive = !folder.NoRecurse
                });
            }

            return builder;
        }
    }
}