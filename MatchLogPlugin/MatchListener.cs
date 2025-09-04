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
            try
            {
                if (e?.Game == null) return;
                string baseDir = string.IsNullOrWhiteSpace(_config.outputPath)
                    ? Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", _config.seasonName)
                    : _config.outputPath;
                string path = baseDir;
                int Id = 0;
                try { Id = RetrieveMatchIDFromFile(path) + 1; } catch (Exception ex) { _logger.LogWarning(ex, "Failed to read last match id; defaulting to 1"); Id = 1; }
                var gameData = new GameData(e.Game.Code, Id);
                if (e.Game.Players != null)
                {
                    foreach (var player in e.Game.Players)
                    {
                        try
                        {
                            if (player?.Character == null) continue;
                            var isImp = player.Character.PlayerInfo?.IsImpostor ?? false;
                            gameData.AddPlayer(player, isImp);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to add player on game start");
                        }
                    }
                }
                gameData.GameStartTimeSet();
                gameDataMap[e.Game.Code] = gameData;
                createMatchLog(e.Game.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onGameStarted failed");
            }
        }

        public void createMatchLog(GameCode gameCode)
        {
            try
            {
                if (!gameDataMap.ContainsKey(gameCode)) return;
                var game = gameDataMap[gameCode];
                string path = string.IsNullOrWhiteSpace(_config.outputPath)
                    ? Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", _config.seasonName)
                    : _config.outputPath;

                string _players = string.Join(",", game.Players.Where(p => p?.Character?.PlayerInfo != null).Select(p => p.Character!.PlayerInfo!.PlayerName));
                // Do not reveal impostors at game start; fill this on game end only
                string _impostors = string.Empty;

                string matchFilePath = Path.Combine(path, $"{game.matchId}_match.json");
                string eventsFilePath = Path.Combine(path, $"{game.matchId}_events.json");
                string movementsFilePath = Path.Combine(path, $"{game.matchId}_movements.json");

                var eventsData = game.JsonifyEventsOnly();
                var movementsData = game.JsonifyRawData();

                var match = new Match {
                    MatchID = game.matchId,
                    GameStarted = game.GameStartedUTC.ToString(),
                    Players = _players,
                    Colors = string.Join(",", game.Players.Where(p => p?.Character?.PlayerInfo != null).Select(p => p.Character!.PlayerInfo!.CurrentOutfit.Color.ToString())),
                    Impostors = _impostors,
                    eventsLogFile = $"{game.matchId}_events.json",
                    MovementsFile = $"{game.matchId}_movements.json",
                    Result = "Unknown",
                    Reason = "Pending"
                };

                var matchjson = JsonSerializer.Serialize(match);
                try { File.WriteAllText(matchFilePath, matchjson); } catch (Exception ex) { _logger.LogError(ex, "Failed writing match file"); }
                try { File.WriteAllText(eventsFilePath, eventsData); } catch (Exception ex) { _logger.LogError(ex, "Failed writing events file"); }
                if (_config.enableReplay)
                {
                    try { File.WriteAllText(movementsFilePath, movementsData); } catch (Exception ex) { _logger.LogError(ex, "Failed writing movements file"); }
                }
                WriteToCsv(gameCode, path, $"{game.matchId}_match.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "createMatchLog failed");
            }
        }

       

        [EventListener(EventPriority.Monitor)]
        public void onGameEnd(IGameEndedEvent e)
        {
            try
            {
                if (e?.Game == null) return;
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                _logger.LogInformation("Game Ended, overwriting files");
                GameCode gamecode = e.Game.Code;

                var gameData = gameDataMap[gamecode];
                var dbEntry = gameData.StringifyData();
                var rawData = gameData.JsonifyRawData();
                var eventsOnly = gameData.JsonifyEventsOnly();
                var resultString = gameData.Canceled ? "Canceled" : getResult(e.GameOverReason);

                string workingDirectory = Environment.CurrentDirectory;
                string directoryPath = string.IsNullOrWhiteSpace(_config.outputPath)
                    ? Path.Combine(workingDirectory, "plugins", "MatchLogs", _config.seasonName)
                    : _config.outputPath;
                string eventsFilePath = Path.Combine(directoryPath, $"{gameData.matchId}_events.json");
                try { File.WriteAllText(eventsFilePath, eventsOnly); } catch (Exception ex) { _logger.LogError(ex, "Failed writing events file (end)"); }
                if (_config.enableReplay)
                {
                    string movementsFilePath = Path.Combine(directoryPath, $"{gameData.matchId}_movements.json");
                    try { File.WriteAllText(movementsFilePath, rawData); } catch (Exception ex) { _logger.LogError(ex, "Failed writing movements file (end)"); }
                }

                var newMatch = new Match {
                    MatchID = gameData.matchId,
                    GameStarted = dbEntry.ElementAtOrDefault(0) ?? string.Empty,
                    Players = dbEntry.ElementAtOrDefault(1) ?? string.Empty,
                    Colors = dbEntry.ElementAtOrDefault(2) ?? string.Empty,
                    Impostors = dbEntry.ElementAtOrDefault(3) ?? string.Empty,
                    eventsLogFile = $"{gameData.matchId}_events.json",
                    MovementsFile = $"{gameData.matchId}_movements.json",
                    Result = resultString,
                    Reason = e.GameOverReason.ToString()
                };

                string matchJson = JsonSerializer.Serialize(newMatch);
                string matchFilePath = Path.Combine(directoryPath, $"{gameData.matchId}_match.json");
                try { File.WriteAllText(matchFilePath, matchJson); } catch (Exception ex) { _logger.LogError(ex, "Failed writing match file (end)"); }

                try { gameDataMap[gamecode].ResetGame(); } catch { }
                gameDataMap.Remove(gamecode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onGameEnd failed");
            }
        }

        [EventListener]
        public void onExile(IMeetingEndedEvent e)
        {
            try
            {
                if (e?.Game == null) return;
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                if (e.Exiled?.PlayerInfo?.PlayerName != null)
                {
                    var exiledName = e.Exiled.PlayerInfo.PlayerName;
                    var now = DateTime.Now;
                    gameDataMap[e.Game.Code].OnExile(exiledName, now);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onExile failed");
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
            try
            {
                if (e?.Game == null || e.Game.GameState == GameStates.NotStarted) return;
                if (e.ClientPlayer?.Character == null) return;
                if (gameDataMap.ContainsKey(e.Game.Code))
                {
                    string playerPosition = e.PlayerControl?.NetworkTransform?.Position.X.ToString() + ", " + e.PlayerControl?.NetworkTransform?.Position.Y.ToString();
                    gameDataMap[e.Game.Code].InMeeting = true;
                    DateTime currentTime = DateTime.Now;
                    TimeSpan timeElapsed = currentTime - gameDataMap[e.Game.Code].GameStartedUTC;
                    var reporter = e.ClientPlayer?.Character?.PlayerInfo?.PlayerName ?? "Unknown";
                    if (e.Body?.PlayerInfo != null)
                    {
                        var dead = e.Body.PlayerInfo.PlayerName ?? "Unknown";
                        gameDataMap[e.Game.Code].OnReport(reporter, dead, playerPosition, timeElapsed);
                    }
                    else
                    {
                        gameDataMap[e.Game.Code].StartMeeting(reporter, timeElapsed);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onReport failed");
            }
        }

        [EventListener]
        public void onMeetingEnd(IMeetingEndedEvent e) {
            try
            {
                if (e?.Game == null) return;
                if (e.Game.GameState == GameStates.NotStarted) return;
                string meetingResult;
                if (e.IsTie) { meetingResult = "Tie"; }
                else if (e.Exiled?.PlayerInfo != null) { meetingResult = e.Exiled.PlayerInfo.PlayerName + " Ejected"; }
                else { meetingResult = "Skipped"; }
                if (gameDataMap.ContainsKey(e.Game.Code))
                {
                    gameDataMap[e.Game.Code].InMeeting = false;
                    TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                    gameDataMap[e.Game.Code].EndMeeting(meetingResult, timeElapsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onMeetingEnd failed");
            }
        }

        [EventListener]
        public async void onPlayerChat(IPlayerChatEvent e)
        {
            try
            {
                if (e?.Game == null) return;
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                if (e.Game.GameState != GameStates.Started) return;

                var message = e.Message?.ToLower()?.Trim();
                if (string.IsNullOrEmpty(message)) return;

                if (message == "/cancel" || message == "?cancel")
                {
                    e.IsCancelled = true;
                    if (!(e.ClientPlayer?.IsHost ?? false)) return;

                    gameDataMap[e.Game.Code].Canceled = true;
                    try { await e.PlayerControl.SendChatToPlayerAsync("Game has been logged as a cancel"); } catch { }
                    await Task.Delay(500);

                    var data = gameDataMap[e.Game.Code];
                    var impostors = data.Impostors ?? new List<IClientPlayer>();

                    var killer = impostors.FirstOrDefault(p => p?.Character != null && !(p.Character.PlayerInfo?.IsDead ?? true));
                    if (killer == null) return;

                    foreach (var player in data.Players.ToList())
                    {
                        try
                        {
                            if (player?.Character == null) continue;
                            if (player.Character.PlayerInfo?.IsDead ?? true) continue;
                            if (impostors.Contains(player)) continue;
                            var killerChar = killer.Character;
                            var targetChar = player.Character;
                            if (killerChar != null && targetChar != null)
                            {
                                await killerChar.ForceMurderPlayerAsync(targetChar, MurderResultFlags.Succeeded);
                            }
                            await Task.Delay(100);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed murdering player during cancel loop");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onPlayerChat failed");
            }
        }


        [EventListener]
        public void onVote(IPlayerVotedEvent e) {
            try
            {
                if (e?.Game == null) return;
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;

                var playerName = e.ClientPlayer?.Character?.PlayerInfo?.PlayerName;
                if (string.IsNullOrWhiteSpace(playerName)) return;

                string voted = e.VotedFor?.PlayerInfo?.PlayerName ?? "none";
                gameDataMap[e.Game.Code].addVote(playerName, DateTime.Now, voted, e.VoteType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onVote failed");
            }
        }

        [EventListener]
        public void OnMovement(IPlayerMovementEvent e)
        {
            try
            {
                if (e?.Game == null || e.Game.GameState == GameStates.NotStarted) return;
                if (e.ClientPlayer?.Character == null) return;
                string playerName = e.ClientPlayer.Character.PlayerInfo?.PlayerName ?? "Unknown";
                bool isDead = e.PlayerControl?.PlayerInfo?.IsDead ?? false;
                var position = e.PlayerControl?.NetworkTransform?.Position ?? default;

                if (gameDataMap.ContainsKey(e.Game.Code) && gameDataMap[e.Game.Code].GameStarted != null)
                {
                    gameDataMap[e.Game.Code].UpdatePlayerPosition(playerName, position, isDead);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OnMovement failed");
            }
        }

        [EventListener]
        public void OnEnterVent(IPlayerEnterVentEvent e)
        {
            try
            {
                if (e?.Game == null || e.Game.GameState == GameStates.NotStarted) return;
                if (e.ClientPlayer?.Character == null) return;
                if (gameDataMap.ContainsKey(e.Game.Code))
                {
                    string playerName = e.ClientPlayer.Character.PlayerInfo?.PlayerName ?? "Unknown";
                    string playerPosition = e.PlayerControl?.NetworkTransform?.Position.X.ToString() + ", " + e.PlayerControl?.NetworkTransform?.Position.Y.ToString();
                    TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                    var ventName = e.Vent?.Name ?? "Unknown";
                    gameDataMap[e.Game.Code].OnEnterVent(playerName, ventName, playerPosition, timeElapsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnEnterVent failed");
            }
        }

        [EventListener]
        public void OnExitVent(IPlayerExitVentEvent e)
        {
            try
            {
                if (e?.Game == null || e.Game.GameState == GameStates.NotStarted) return;
                if (e.ClientPlayer?.Character == null) return;
                if (gameDataMap.ContainsKey(e.Game.Code))
                {
                    string playerName = e.ClientPlayer.Character.PlayerInfo?.PlayerName ?? "Unknown";
                    string playerPosition = e.PlayerControl?.NetworkTransform?.Position.X.ToString() + ", " + e.PlayerControl?.NetworkTransform?.Position.Y.ToString();
                    TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                    var ventName = e.Vent?.Name ?? "Unknown";
                    gameDataMap[e.Game.Code].OnExitVent(playerName, ventName, playerPosition, timeElapsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnExitVent failed");
            }
        }

        [EventListener]
        public void onPlayerMurder(IPlayerMurderEvent e) {
            try
            {
                if (e?.Game == null || e.Game.GameState == GameStates.NotStarted) return;
                if (e.ClientPlayer?.Character == null || e.Victim?.PlayerInfo == null) return;
                var playerKilled = e.Victim.PlayerInfo.PlayerName ?? "Unknown";
                var killer = e.ClientPlayer.Character.PlayerInfo?.PlayerName ?? "Unknown";
                string playerPosition = e.PlayerControl?.NetworkTransform?.Position.X.ToString() + ", " + e.PlayerControl?.NetworkTransform?.Position.Y.ToString();
                var currentGame = e.Game.Code;
                DateTime dateTime = DateTime.Now;
                if (gameDataMap.ContainsKey(currentGame))
                {
                    TimeSpan timeElapsed = dateTime - gameDataMap[currentGame].GameStartedUTC;
                    gameDataMap[currentGame].OnDeathEvent(playerKilled, killer, playerPosition, timeElapsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onPlayerMurder failed");
            }
        }

        
        [EventListener(EventPriority.Monitor)]
        public void onTaskCompletion(IPlayerCompletedTaskEvent e) {
            try
            {
                if (e?.ClientPlayer?.Character == null || e.Task?.Task == null) return;
                if (e?.Game == null) return;
                var currentGame = e.Game.Code;
                if (!gameDataMap.ContainsKey(currentGame)) return;
                string playerName = e.ClientPlayer.Character.PlayerInfo?.PlayerName ?? "Unknown";
                var taskType = e.Task.Task.Type.ToString();
                string playerPosition = e.PlayerControl?.NetworkTransform?.Position.X.ToString() + ", " + e.PlayerControl?.NetworkTransform?.Position.Y.ToString();
                DateTime dateTime = DateTime.Now;
                if (e.Task.Complete)
                {
                    TimeSpan timeElapsed = dateTime - gameDataMap[currentGame].GameStartedUTC;
                    gameDataMap[currentGame].OnTaskFinish(playerName, taskType, playerPosition, timeElapsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onTaskCompletion failed");
            }
        }


        [EventListener]
        public void onDisconnection(IGamePlayerLeftEvent e)
        {
            try
            {
                if (e?.Game == null) return;
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                var gd = gameDataMap[e.Game.Code];
                var now = DateTime.Now;
                var playerName = e.Player?.Character?.PlayerInfo?.PlayerName ?? "Unknown";
                gd.OnDisconnect(playerName, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onDisconnection failed");
            }
        }

        private int RetrieveMatchIDFromFile(string path)
        {
            try
            {
                string csvFilePath = Path.Combine(path, "matches.csv");
                var matchId = 0;
                if (File.Exists(csvFilePath))
                {
                    using var file = File.Open(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using StreamReader streamReader = new StreamReader(file);
                    var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
                    var records = csvReader.GetRecords<Season>().ToList();
                    if (records.Count > 0)
                    {
                        matchId = records.Max(r => r.Id);
                    }
                }
                return matchId;
            }
            catch
            {
                return 0;
            }
        }

        private void WriteToCsv(GameCode gameCode, string path, string matchFileName)
        {
            try
            {
                if (!gameDataMap.ContainsKey(gameCode)) return;
                string csvFilePath = Path.Combine(path, "matches.csv");
                var game = gameDataMap[gameCode];
                if(File.Exists(csvFilePath)) 
                {
                    _logger.LogInformation($"MatchId: {game.matchId} has been logged");
                    using var file = File.Open(csvFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using StreamWriter writer = new StreamWriter(file);
                    var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

                    Season season = new Season();
                    season.Id = game.matchId;
                    season.Match = matchFileName;
                    
                    csvWriter.WriteRecord(season);
                    csvWriter.NextRecord();
                    csvWriter.Flush();
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WriteToCsv failed");
            }
            
        }

    }
}
