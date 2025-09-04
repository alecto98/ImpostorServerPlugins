using CsvHelper;
using CsvHelper.Configuration;
using Impostor.Api.Events.Managers;
using Impostor.Api.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace MatchLog
{
    [ImpostorPlugin(id: "sanctum.matchlog")]
    public class MatchLog : PluginBase
    {
        public readonly ILogger<MatchLog> _logger;
        public readonly IEventManager _eventmanager;
        private IDisposable? _unregister;
        private readonly double version = 1.1;
        public Config config;

        public MatchLog(ILogger<MatchLog> logger, IEventManager eventManager) {
            _logger = logger;
            _eventmanager = eventManager;
            config = LoadConfig();
        }

        public override ValueTask EnableAsync()
        {

            string dataDirectory = Environment.CurrentDirectory;
            string seasonName = config.seasonName;

            string directoryPath = string.IsNullOrWhiteSpace(config.outputPath)
                ? Path.Combine(dataDirectory, "plugins", "MatchLogs", seasonName)
                : config.outputPath;
            bool directoryExists = Directory.Exists(directoryPath);

            if (!directoryExists) {
                Directory.CreateDirectory(directoryPath);
            }

            var filePath = Path.Combine(directoryPath, "matches.csv");
            if (!File.Exists(filePath))
            {
                CreateCSV(filePath, directoryPath);
            }

            _logger.LogInformation($"MatchLog {version} enabled!");
            _unregister = _eventmanager.RegisterListener(new MatchListener(_logger, _eventmanager, config));
            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation($"MatchLog {version} disabled");
            _unregister?.Dispose();
            return default;
        }

        private void CreateCSV(string path, string directoryPath)
        {

            FileStream filestream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            StreamWriter writer = new StreamWriter(filestream);
            CsvConfiguration csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };
            CsvWriter csvWriter = new CsvWriter(writer, csvConfiguration);
            csvWriter.WriteHeader<Season>();
            csvWriter.NextRecord();

            var records = new List<Season>();
            var sorted_matches = RetrieveMatches(directoryPath).OrderBy(m => DateTime.Parse(m.GameStarted)).ToList();
            
            foreach(Match match in sorted_matches) 
            {
                Season season = new Season();
                season.Id = match.MatchID;
                season.Match = $"{match.MatchID}_match.json";
                records.Add(season);
            }
            csvWriter.WriteRecords<Season>(records);
            writer.Close();
            filestream.Close();

        }

        public List<Match> RetrieveMatches(string directoryPath)
        {
            var matches = new List<Match>();
            foreach(string filename in Directory.GetFiles(directoryPath, "*_match.json"))
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var m = JsonSerializer.Deserialize<Match>(File.ReadAllText(filename), options);
                    if (m != null) matches.Add(m);
                } catch(Exception e)
                {
                    _logger.LogError($"File: {filename}");
                    _logger.LogError(e.Message);
                }
            }
            return matches;
        }

        private Config LoadConfig()
        {
            var configDir = Path.Combine(Environment.CurrentDirectory, "config");
            var filePath = Path.Combine(configDir, "matchLogConfig.json");
            var options = new JsonSerializerOptions { WriteIndented = true };

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            Config config;
            if (!File.Exists(filePath))
            {
                // File didn't exist: write defaults with full pretty JSON
                config = new Config { seasonName = "Preseason", enableReplay = true, outputPath = string.Empty };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(filePath, json);
                return config;
            }

            // File exists: read it and try to deserialize
            try
            {
                var content = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(content) || content.Trim() == "{}")
                {
                    // File exists but empty: populate it now
                    config = new Config { seasonName = "Preseason", enableReplay = true, outputPath = string.Empty };
                    var json = JsonSerializer.Serialize(config, options);
                    File.WriteAllText(filePath, json);
                    return config;
                }
                config = JsonSerializer.Deserialize<Config>(content) ?? new Config { seasonName = "Preseason", enableReplay = true, outputPath = string.Empty };
                return config;
            }
            catch
            {
                // On invalid JSON: return defaults without overwriting
                return new Config { seasonName = "Preseason", enableReplay = true, outputPath = string.Empty };
            }
        }

    }
}