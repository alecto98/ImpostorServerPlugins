
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GameTimerPlugin
{
    //[ImpostorPlugin(
    //  id: "vex.GameTimerPlugin",
    //name: "GameTimerPlugin",
    //author: "vex",
    //version: "2.0.0")]
    [ImpostorPlugin(
        id: "xsoul.GameTimerPlugin"
        )]
    public class GameTimerPlugin : PluginBase
    {
        public readonly ILogger<GameTimerPlugin> _logger;
        public readonly IEventManager _eventManager;

        private IDisposable? _unregister;

        public GameTimerPlugin(ILogger<GameTimerPlugin> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("GameTimerPlugin enabled!");
            _unregister = _eventManager.RegisterListener(new TimerPlugin(_logger));
            return default;
        }
    }
}