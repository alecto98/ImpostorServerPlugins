using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Replay
{
    public class MatchListener : IEventListener
    {
        private readonly ILogger<Replay> _logger;
        private IEventManager _eventManager;
        private Dictionary<GameCode, GameData> gameDataMap = new();

        public MatchListener(ILogger<Replay> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            var gameData = new GameData(e.Game.Code);
            foreach (var player in e.Game.Players)
            {
                if (player.Character == null) return;
                gameData.AddPlayer(player, player.Character.PlayerInfo.IsImpostor);
            }
            gameData.GameStartTimeSet();
            gameDataMap.Add(e.Game.Code, gameData);
        }

        [EventListener]
        public void OnGameEnd(IGameEndedEvent e)
        {
            if (e.Game == null) return;
            GameCode gamecode = e.Game.Code;

            if (gameDataMap.ContainsKey(gamecode))
            {
                var gameData = gameDataMap[gamecode];
                var dbEntry = gameData.StringifyData();
                var rawData = gameData.JsonifyRawData();
                var resultString = gameData.Canceled ? "Canceled" : GetResult(e.GameOverReason);
                int matchID = IncrementMatchCount();
                string matchIDString = matchID.ToString();
                string directoryPath = Config.GetConfigValue("directoryPath");

                if (string.IsNullOrEmpty(directoryPath))
                {
                    directoryPath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs");
                }

                string movementsFilePath = Path.Combine(directoryPath, $"{matchIDString}_movements.json");
                string matchFilePath = Path.Combine(directoryPath, $"{matchIDString}_match.json");

                File.WriteAllText(movementsFilePath, rawData);

                var newMatch = new Match
                {
                    MatchID = matchID,
                    GameStarted = dbEntry[0],
                    Players = dbEntry[1],
                    Colors = dbEntry[2],
                    Impostors = dbEntry[3],
                    MovementsFile = $"{matchIDString}_movements.json",
                    Result = resultString,
                    Reason = e.GameOverReason.ToString()
                };

                string matchJson = JsonSerializer.Serialize(newMatch);
                File.WriteAllText(matchFilePath, matchJson);

                gameDataMap[gamecode].ResetGame();
                gameDataMap.Remove(gamecode);
            }
        }


        [EventListener]
        public void OnMovement(IPlayerMovementEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer == null || e.ClientPlayer.Character == null) return;

            try
            {
                string playerName = e.ClientPlayer.Character.PlayerInfo.PlayerName;
                DateTime currentTime = DateTime.Now;

                if (gameDataMap.ContainsKey(e.Game.Code) && gameDataMap[e.Game.Code].GameStarted != null && !e.PlayerControl.PlayerInfo.IsDead)
                {
                    if (!lastMovementTime.ContainsKey(playerName) ||
                        (currentTime - lastMovementTime[playerName]).TotalSeconds > 0.2)  // 0.2 second delay
                    {
                        string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();

                        if (gameDataMap.ContainsKey(e.Game.Code))
                        {
                            gameDataMap[e.Game.Code].OnMovement(playerName, playerPosition, currentTime - gameDataMap[e.Game.Code].GameStartedUTC);
                            lastMovementTime[playerName] = currentTime;  // Update the last movement time
                        }
                    }
                }
            }
            catch (KeyNotFoundException ex)
            {
                //_logger.LogWarning($"Error processing movement for player {e.ClientPlayer.Character.PlayerInfo.PlayerName}: The given key was not present in the dictionary. {ex.Message}");
            }
            catch (Exception ex)
            {
                //_logger.LogWarning($"Unexpected error processing movement for player {e.ClientPlayer.Character.PlayerInfo.PlayerName}: {ex.Message}", ex);
            }
        }

        [EventListener]
        public void OnTaskCompletion(IPlayerCompletedTaskEvent e)
        {
            if (e.ClientPlayer.Character == null || e.Task == null) return;

            var player = e.ClientPlayer;
            var currentGame = e.Game.Code;
            string playerName = e.ClientPlayer.Character.PlayerInfo.PlayerName;
            var taskType = e.Task.Task.Type.ToString();
            string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();
            DateTime dateTime = DateTime.Now;
            if (e.Task.Complete)
            {
                TimeSpan timeElapsed = dateTime - gameDataMap[currentGame].GameStartedUTC;
                gameDataMap[player.Game.Code].OnTaskFinish(playerName, taskType, playerPosition, timeElapsed);
            }
        }

        public string GetResult(GameOverReason reason)
        {
            List<GameOverReason> crew = new() { GameOverReason.HumansByVote, GameOverReason.HumansByTask };
            List<GameOverReason> imp = new() { GameOverReason.ImpostorByKill, GameOverReason.ImpostorBySabotage, GameOverReason.ImpostorByVote };
            return crew.Contains(reason) ? "Crewmates Win" : imp.Contains(reason) ? "Impostors Win" : "Unknown";
        }

        [EventListener]
        public void OnReport(IPlayerStartMeetingEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer == null || e.ClientPlayer.Character == null) return;

            if (gameDataMap.ContainsKey(e.Game.Code))
            {
                string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();
                gameDataMap[e.Game.Code].InMeeting = true;
                DateTime currentTime = DateTime.Now;
                TimeSpan timeElapsed = currentTime - gameDataMap[e.Game.Code].GameStartedUTC;
                if (e.Body != null)
                {
                    gameDataMap[e.Game.Code].OnReport(e.ClientPlayer.Character.PlayerInfo.PlayerName, e.Body.PlayerInfo.PlayerName, playerPosition, timeElapsed);
                }
                else
                {
                    gameDataMap[e.Game.Code].StartMeeting(e.ClientPlayer.Character.PlayerInfo.PlayerName, timeElapsed);
                }
            }
        }

        private Dictionary<string, DateTime> lastMovementTime = new();

        [EventListener]
        public void OnEnterVent(IPlayerEnterVentEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer == null || e.ClientPlayer.Character == null) return;

            if (gameDataMap.ContainsKey(e.Game.Code))
            {
                string playerName = e.ClientPlayer.Character.PlayerInfo.PlayerName;
                string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();
                TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                gameDataMap[e.Game.Code].OnEnterVent(playerName, e.Vent.Name, playerPosition, timeElapsed);
            }
        }


        [EventListener]
        public void OnExitVent(IPlayerExitVentEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer == null || e.ClientPlayer.Character == null) return;

            if (gameDataMap.ContainsKey(e.Game.Code))
            {
                string playerName = e.ClientPlayer.Character.PlayerInfo.PlayerName;
                string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();
                TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                gameDataMap[e.Game.Code].OnExitVent(playerName, e.Vent.Name, playerPosition, timeElapsed);
            }
        }


        [EventListener]
        public void OnMeetingEnd(IMeetingEndedEvent e)
        {
            if (e.Game == null) return;
            if (e.Game.GameState == GameStates.NotStarted) return;

            string meetingResult;
            if (e.Exiled != null) { meetingResult = e.Exiled.ToString() + " Ejected"; }
            else { meetingResult = "Skipped"; }

            if (gameDataMap.ContainsKey(e.Game.Code))
            {
                gameDataMap[e.Game.Code].InMeeting = false;
                TimeSpan timeElapsed = DateTime.Now - gameDataMap[e.Game.Code].GameStartedUTC;
                gameDataMap[e.Game.Code].EndMeeting(meetingResult, timeElapsed);
            }
        }


        [EventListener]
        public void OnPlayerMurder(IPlayerMurderEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer == null || e.ClientPlayer.Character == null || e.Victim == null) return;

            var playerKilled = e.Victim.PlayerInfo.PlayerName;
            var killer = e.ClientPlayer.Character.PlayerInfo.PlayerName;
            string playerPosition = e.PlayerControl.NetworkTransform.Position.X.ToString() + ", " + e.PlayerControl.NetworkTransform.Position.Y.ToString();
            var currentGame = e.Game.Code;

            DateTime dateTime = DateTime.Now;

            if (gameDataMap.ContainsKey(currentGame))
            {
                TimeSpan timeElapsed = dateTime - gameDataMap[currentGame].GameStartedUTC;
                gameDataMap[currentGame].OnDeathEvent(playerKilled, killer, playerPosition, timeElapsed);
            }
        }

        private int IncrementMatchCount()
        {
            string matchesPath = Config.GetConfigValue("matchesPath");

            if (string.IsNullOrEmpty(matchesPath))
            {
                matchesPath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs", "Preseason");
            }

            if (!Directory.Exists(matchesPath))
            {
                Directory.CreateDirectory(matchesPath);
            }

            string matchFilePattern = "*.json";
            string[] matchFiles = Directory.GetFiles(matchesPath, matchFilePattern);

            int matchCount = 0;

            if (matchFiles.Length > 0)
            {
                foreach (var matchFile in matchFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(matchFile);
                    if (int.TryParse(fileName.Split('_')[0], out int matchId))
                    {
                        if (matchId > matchCount)
                        {
                            matchCount = matchId;
                        }
                    }
                }
            }

            return matchCount;
        }
    }
}
