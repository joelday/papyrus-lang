using DarkId.Papyrus.Server.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Features
{
    [Method("textDocument/registryHandler")]
    [Parallel]
    public class RegistryHandler : IRegistryHandler
    {
        private readonly ILogger _logger;

        public RegistryHandler(ILogger<RegistryHandler> logger)
        {
            _logger = logger;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = Constants.PapyrusScriptSelector
            };
        }

        public Task<RegistryHandlerInfo> Handle(RegistryHandlerParams request, CancellationToken cancellationToken)
        {
            try
            {
                string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Bethesda Softworks\" + request.RegKeyName;
                string KeyValue = (string)Registry.GetValue(keyPath, "Installed Path", null);

                return Task.FromResult<RegistryHandlerInfo>(new RegistryHandlerInfo()
                {
                    Value = KeyValue,
                    Exists = (KeyValue != null)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while handling request.");
            }
            return Task.FromResult<RegistryHandlerInfo>(null);
        }

        public void SetCapability(RegistryHandlerCapability capability)
        {
            capability.DynamicRegistration = true;
        }
    }
}
