using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.LanguageService.Configuration.CreationKit;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Newtonsoft.Json.Linq;
using DarkId.Papyrus.Common;
using Microsoft.Extensions.Logging;

namespace DarkId.Papyrus.Server
{
    class CreationKitInisLocator : ICreationKitInisLocator
    {
        private readonly CreationKitIniLocations _locations;

        public CreationKitInisLocator(CreationKitIniLocations locations)
        {
            _locations = locations;
        }

        public CreationKitIniLocations GetIniLocations()
        {
            return this._locations;
        }
    }
}
