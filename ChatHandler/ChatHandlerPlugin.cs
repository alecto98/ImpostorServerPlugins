using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatHandlerPlugin
{
    [ImpostorPlugin(id: "dixter.auc.chathandler")]
    public class ChatHandlerPlugin : PluginBase
    {
        private readonly ILogger<ChatHandlerPlugin> _logger;
        private readonly IEventManager _eventManager;
        private IDisposable _unregister = null!;

        private string ConfigDirectoryPath = Path.Combine(Environment.CurrentDirectory, "config");
        private const string ConfigPath = "chathandler.json";

        public ChatHandlerPlugin(ILogger<ChatHandlerPlugin> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        public Config LoadConfig()
        {
            string config_path = Path.Combine(ConfigDirectoryPath, ConfigPath);
            Config config;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            if (File.Exists(config_path))
            {
                string jsonString = File.ReadAllText(config_path);
                config = JsonSerializer.Deserialize<Config>(jsonString, options) ?? new Config();
            }
            else
            {
                config = new Config();
                string jsonString = JsonSerializer.Serialize(config, options);
                File.WriteAllText(config_path, jsonString);
            }
            return config;
        }

        public override ValueTask EnableAsync()
        {
            Directory.CreateDirectory(ConfigDirectoryPath);
            var config = LoadConfig();

            _logger.LogInformation("ChatHandlerPlugin is enabled.");
            _unregister = _eventManager.RegisterListener(new ChatHandlerListener(_logger, config));
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("ChatHandlerPlugin is disabled.");
            _unregister.Dispose();
            return default;
        }
    }
}