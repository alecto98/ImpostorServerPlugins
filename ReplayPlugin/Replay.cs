using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Replay
{
    [ImpostorPlugin(id: "aiden.auc.replay")]
    public class Replay : PluginBase
    {
        public readonly ILogger<Replay> _logger;
        public readonly IEventManager _eventmanager;
        private IDisposable _unregister;
        private readonly double version = 1.01;

        public Replay(ILogger<Replay> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventmanager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            string configFilePath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", "config.txt");
            Config.LoadConfig();

            string dataDirectory = Config.GetConfigValue("directoryPath");

            if (string.IsNullOrEmpty(dataDirectory))
            {
                dataDirectory = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs");
            }

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            _logger.LogInformation($"Replay {version} enabled!");
            _unregister = _eventmanager.RegisterListener(new MatchListener(_logger, _eventmanager));
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation($"Replay {version} disabled");
            _unregister.Dispose();
            return default;
        }
    }

    public static class Config
    {
        private static Dictionary<string, string> _config = new();
        private static string configFilePath = Path.Combine(Environment.CurrentDirectory, "plugins", "config_replay.txt");
        private static string defaultDirectoryPath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", "Replay");
        private static string defaultMatchesPath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", "Preseason");

        public static void LoadConfig()
        {
            // Create the config file if it doesn't exist
            if (!File.Exists(configFilePath))
            {
                CreateConfigFile();
            }

            // Load the config values from the file
            foreach (var line in File.ReadAllLines(configFilePath))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    _config[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        public static string GetConfigValue(string key)
        {
            return _config.TryGetValue(key, out var value) ? value : null;
        }

        public static void CreateConfigFile()
        {
            // Ensure the directory exists
            string directory = Path.Combine(Environment.CurrentDirectory, "plugins");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create the config file and store the default paths
            File.WriteAllText(configFilePath, $"directoryPath={defaultDirectoryPath}\n");
            File.AppendAllText(configFilePath, $"matchesPath={defaultMatchesPath}\n");
            Console.WriteLine($"Config file created at {configFilePath} with default paths.");
        }
    }

}
