using DarkId.Papyrus.Common;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server
{
    public class WorkspaceConfiguration
    {
        public string InstallPath { get; set; }
        public List<string> CreationKitIniFiles { get; set; }
        public string CompilerAssemblyPath { get; set; }
    }

    public static class WorkspaceConfigurationExtensions
    {
        public static WorkspaceConfiguration GetConfiguration(this ILanguageServerWorkspace workspace)
        {
            var configuration = workspace.SendRequest<ConfigurationParams, Container<JToken>>(
                WorkspaceNames.WorkspaceConfiguration,
                new ConfigurationParams()
                {
                    Items = new ConfigurationItem[]
                    {
                        new ConfigurationItem()
                        {
#if FALLOUT4
                            Section = "papyrus.fallout4"
#elif SKYRIM
                            Section = "papyrus.skyrim"
#endif
                        }
                    }
                })
                .WaitForResult()
                .FirstOrDefault();

            return configuration?.ToObject<WorkspaceConfiguration>();
        }
    }
}
