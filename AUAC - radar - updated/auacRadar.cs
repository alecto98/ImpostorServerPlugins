using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using System;

namespace AmongUsRadarAntiCheat
{
    [ImpostorPlugin("Aiden.AC.Radar")]
    public class auacRadar : PluginBase 
    {
        private readonly ILogger<auacRadarListener> _logger;
        private readonly IEventManager _eventManager;
        private IDisposable? _unregister;
        private auacRadarListener? _listener;

        public auacRadar(ILogger<auacRadarListener> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogDebug("Radar Anti-Cheat is enabled.");
            _listener = new auacRadarListener(_logger, _eventManager);
            _unregister = _eventManager.RegisterListener(_listener);
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogDebug("Radar Anti-Cheat is disabled.");
            _unregister?.Dispose();
            _listener?.Dispose();
            return default;
        }
    }
}
