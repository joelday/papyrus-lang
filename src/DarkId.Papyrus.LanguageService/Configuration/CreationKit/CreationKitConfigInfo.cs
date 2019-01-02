using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitConfigInfo
    {
        public string CreationKitInstallPath { get; set; }
        public CreationKitConfig Config { get; set; }
    }
}