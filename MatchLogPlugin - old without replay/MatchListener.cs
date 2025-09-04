using CsvHelper;
using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Custom;
using Impostor.Api.Net.Inner;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Inner.Objects.ShipStatus;
using Impostor.Api.Net.Messages.Rpcs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MatchLog
{
    public class MatchListener : IEventListener
    {

        private readonly ILogger<MatchLog> _logger;
        private IEventManager _eventManager;
        private Dictionary<GameCode, GameData> gameDataMap = new();
        private Config _config;
        public MatchListener(ILogger<MatchLog> logger, IEventManager eventManager, Config config) {
            _logger = logger;
            _eventManager = eventManager;
            _config = config;
        }

        [EventListener]
        public void onGameStarted(IGameStartedEvent e) {
            string path = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", _config.seasonName);
            int Id = RetrieveMatchIDFromFile(path) + 1;
            var gameData = new GameData(e.Game.Code, Id);
            foreach (var player in e.Game.Players) {
                if (player.Character == null) return;
                gameData.AddPlayer(player, player.Character.PlayerInfo.IsImpostor);
            }
            gameData.gameStartTimeSet();
            gameDataMap.Add(e.Game.Code, gameData);

            createMatchLog(e.Game.Code);
        }

        public void createMatchLog(GameCode gameCode)
        {
            if (!gameDataMap.ContainsKey(gameCode)) return;
            var game = gameDataMap[gameCode];
            string path = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", _config.seasonName);

            string _players = string.Join(",", game.Players.Select(p => p.Character.PlayerInfo.PlayerName));
            string _impostors = string.Join(",", game.Impostors.Select(p => p.Character.PlayerInfo.PlayerName));

            string matchFilePath = Path.Combine(path, $"{game.matchId}_match.json");
            string eventsFilePath = Path.Combine(path, $"{game.matchId}_events.json");

            var eventsData = game.jsonifyRawData();

            var match = new Match {
                MatchID = game.matchId,
                gameStarted = game.gameStartedUTC.ToString(),
                players = _players,
                impostors = _impostors,
                eventsLogFile = $"{game.matchId}_events.json",
                result = "Unknown",
                reason = "Pending"
            };

            var matchjson = JsonSerializer.Serialize(match);
            File.WriteAllText(matchFilePath, matchjson);
            File.WriteAllText(eventsFilePath, eventsData);
            WriteToCsv(gameCode, path, $"{game.matchId}_match.json");
        }

       

        [EventListener(EventPriority.Monitor)]
        public void onGameEnd(IGameEndedEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            _logger.LogInformation("Game Ended, overwriting files");
            if (e.Game == null) return;
            GameCode gamecode = e.Game.Code;

            //get all data
            var gameData = gameDataMap[gamecode];
            var dbEntry = gameData.stringifyData();
            var rawData = gameData.jsonifyRawData();
            var resultString = gameData.canceled ? "Canceled" : getResult(e.GameOverReason);

            //Write logfile to directory
            string workingDirectory = Environment.CurrentDirectory;
            string directoryPath = Path.Combine(workingDirectory, "plugins", "MatchLogs", _config.seasonName);
            string filePath = Path.Combine(directoryPath, $"{gameData.matchId}_events.json");
            File.WriteAllText(filePath, rawData);

            var newMatch = new Match {
                MatchID = gameData.matchId,
                gameStarted = dbEntry[0],
                players = dbEntry[1],
                impostors = dbEntry[2],
                eventsLogFile = $"{gameData.matchId}_events.json",
                result = resultString,
                reason = e.GameOverReason.ToString()
            };

            string matchJson = JsonSerializer.Serialize(newMatch);
            string matchFilePath = Path.Combine(directoryPath, $"{gameData.matchId}_match.json");
            File.WriteAllText(matchFilePath, matchJson);

            gameDataMap[gamecode].ResetGame();
            gameDataMap.Remove(gamecode);


        }

        [EventListener]
        public void onExile(IMeetingEndedEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            if (e.Exiled != null)
            {
                gameDataMap[e.Game.Code].onExile(e.Exiled.PlayerInfo.PlayerName, DateTime.Now);
            }
        }

        public string getResult(GameOverReason reason)
        {
            List<GameOverReason> crew = new List<GameOverReason> { GameOverReason.HumansByVote, GameOverReason.HumansByTask};
            List<GameOverReason> imp = new List<GameOverReason> { GameOverReason.ImpostorByKill, GameOverReason.ImpostorBySabotage, GameOverReason.ImpostorByVote};

            string result = "Unknown";
            if(crew.Contains(reason))
            {
                result = "Crewmates Win";
            } else if(imp.Contains(reason))
            {
                result = "Impostors Win";
            }
            return result;
        }

        [EventListener]
        public void onReport(IPlayerStartMeetingEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;

            gameDataMap[e.Game.Code].inMeeting = true;
            bool bodyreport = false;
            if(e.ClientPlayer == null)
            {
                //Should never happen
                return;
            }
            if (e.Body != null) {
                bodyreport = true;
            }
            if (bodyreport) {
                if(e.ClientPlayer.Character != null && e.Body != null)
                gameDataMap[e.Game.Code].onReport(e.ClientPlayer.Character.PlayerInfo.PlayerName, e.Body.PlayerInfo.PlayerName, DateTime.Now);
            } else if(e.ClientPlayer.Character != null)
            {
                gameDataMap[e.Game.Code].startMeeting(e.ClientPlayer.Character.PlayerInfo.PlayerName, DateTime.Now);
            } else
            {
                //Error
                return;
            }
        }

        [EventListener]
        public void onMeetingEnd(IMeetingEndedEvent e) {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;

            string meetingResult = "Skipped";
            if (e.IsTie) {
                meetingResult = "Tie";
            }
            if(e.Exiled != null)
            {
                meetingResult = "Exiled";
            }
            gameDataMap[e.Game.Code].inMeeting = false;
            gameDataMap[e.Game.Code].endMeeting(DateTime.Now, meetingResult);
        }

        [EventListener]
        public async void onPlayerChat(IPlayerChatEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            if (e.Game.GameState != GameStates.Started) return;

            var message = e.Message.ToLower().Trim();

            if (message == "/cancel" || message == "?cancel")
            {
                e.IsCancelled = true;
                if (!e.ClientPlayer.IsHost) return;

                gameDataMap[e.Game.Code].canceled = true;
                gameDataMap[e.Game.Code].onCancel(e.PlayerControl.PlayerInfo.PlayerName, DateTime.Now);
                await e.PlayerControl.SendChatToPlayerAsync("Game has been logged as a cancel");
                await Task.Delay(500);

                gameDataMap[e.Game.Code].onEnd(e.ClientPlayer, DateTime.Now);

                var data = gameDataMap[e.Game.Code];
                var impostors = data.Impostors;

                // Find the first alive impostor
                var killer = impostors.FirstOrDefault(p => p.Character != null && !p.Character.PlayerInfo.IsDead);

                if (killer == null) return; // No alive impostor found

                foreach (var player in data.Players)
                {
                    if (player.Character == null) continue;
                    if (player.Character.PlayerInfo.IsDead) continue;
                    if (impostors.Contains(player)) continue; // Don't kill impostors

                    await killer.Character.MurderPlayerAsync(player.Character);
                    await Task.Delay(100); // Small delay to avoid overloading the server
                }
            }
        }


        [EventListener]
        public void onVote(IPlayerVotedEvent e) {

            if (!gameDataMap.ContainsKey(e.Game.Code)) return;

            string voted = "none";
            string player = "none";
            if (e.VotedFor != null) {
                voted = e.VotedFor.PlayerInfo.PlayerName;
            }
            if(e.ClientPlayer.Character != null)
            {
                player = e.ClientPlayer.Character.PlayerInfo.PlayerName;
            }

            gameDataMap[e.Game.Code].addVote(player,DateTime.Now, voted, e.VoteType);
        }

        [EventListener]
        public void onPlayerMurder(IPlayerMurderEvent e) {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;

            var playerKilled = e.Victim;
            var currentGame = e.Game.Code;
            var killedClient = e.Game.Players.FirstOrDefault(p => p.Character == playerKilled);
            var killer = e.ClientPlayer;
            DateTime dateTime = DateTime.Now;

            if (killedClient != null && gameDataMap.ContainsKey(currentGame))
            {
                gameDataMap[currentGame].onDeathEvent(killedClient, dateTime, killer);
            }
        }

        
        [EventListener]
        public void onTaskCompletion(IPlayerCompletedTaskEvent e) {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;

            if (e.ClientPlayer.Character == null) return;
            var player = e.ClientPlayer;
            var task = e.Task;
            var timeOfcompletion = DateTime.UtcNow;

            if (task.Complete) {
                gameDataMap[player.Game.Code].addTaskCompletion(player, timeOfcompletion, task.Task.Category, task.Task.Type);
            }
        }


        [EventListener]
        public void onDisconnection(IGamePlayerLeftEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            DateTime dateTime = DateTime.Now;
            gameDataMap[e.Game.Code].disconnectedPlayer(dateTime, e.Player);
        }

        private int RetrieveMatchIDFromFile(string path)
        {
            string csvFilePath = Path.Combine(path, "matches.csv");
            var matchId = 0;
            if (File.Exists(csvFilePath))
            {
                //List<Season> records = new List<Season>();
                var file = File.Open(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader streamReader = new StreamReader(file);
                CsvReader csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
                var records = csvReader.GetRecords<Season>().ToList();

                if(records.Count > 0) 
                {
                    matchId = records.Max(r => r.Id);
                }

                streamReader.Close();
                file.Close();
            }
            return matchId;
        }

        private void WriteToCsv(GameCode gameCode, string path, string matchFileName)
        {
            if (!gameDataMap.ContainsKey(gameCode)) return;
            string csvFilePath = Path.Combine(path, "matches.csv");
            var game = gameDataMap[gameCode];
            if(File.Exists(csvFilePath)) 
            {
                _logger.LogInformation($"MatchId: {game.matchId} has been logged");
                var file = File.Open(csvFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                StreamWriter writer = new StreamWriter(file);
                CsvWriter csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

                Season season = new Season();
                season.Id = game.matchId;
                season.Match = matchFileName;
                
                csvWriter.WriteRecord(season);
                csvWriter.NextRecord();
                csvWriter.Flush();
                writer.Flush();
                writer.Close();
                file.Close();
            }
           
        }

    }
}
