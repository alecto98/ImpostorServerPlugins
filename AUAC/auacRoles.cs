using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using System;

namespace AmongUsRolesAntiCheat
{
    [ImpostorPlugin(
        id: "Aiden.AC.Roles",
        name: "Roles Anti-Cheat",
        author: "Aiden",
        version: "1.0.0")]
    [Obsolete]
    public class auacRoles : PluginBase 
    {
        private readonly ILogger<auacRoles> _logger;
        private readonly ILogger<auacRolesListener> _listenerLogger;
        public readonly IEventManager _eventManager;
        private IDisposable? _unregister;

        public auacRoles(ILogger<auacRoles> logger, ILogger<auacRolesListener> listenerLogger, IEventManager eventManager)
        {
            _logger = logger;
            _listenerLogger = listenerLogger;
            _eventManager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogDebug("Roles Anti-Cheat is enabled.");
            _unregister = _eventManager.RegisterListener(new auacRolesListener(_listenerLogger, _eventManager));
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogDebug("Roles Anti-Cheat is disabled.");
            _unregister?.Dispose();
            return default;
        }
    }
}
