using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using System;

namespace DiscordBot
{
    [ImpostorPlugin(
        id: "dixter.auc.allowedVersions",
        name: "allowedVersions",
        author: "Aiden",
        version: "1.0.0")]
    [Obsolete]
    public class AllowedVersions : PluginBase // This is also required ": PluginBase".
    {
        private readonly ILogger<AllowedVersions> _logger;
        public readonly IEventManager _eventManager;
        private IDisposable _unregister;

        public AllowedVersions(ILogger<AllowedVersions> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("allowedVersions is enabled.");
            _unregister = _eventManager.RegisterListener(new allowedVersionsListener(_logger, _eventManager));
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("allowedVersions is disabled.");
            return default;
        }
    }
}
