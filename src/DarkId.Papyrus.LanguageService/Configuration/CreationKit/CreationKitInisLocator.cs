using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Configuration.CreationKit
{
    public class CreationKitInisLocator : ICreationKitInisLocator
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
