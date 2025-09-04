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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.ComponentModel.DataAnnotations;
using Impostor.Api.Config;
using Impostor.Api;
using Impostor.Api.Innersloth.GameOptions;

namespace DiscordBot
{
    public class DiscordBotListener : IEventListener
    {
        private readonly ILogger<DiscordBot> _logger;
        private readonly string hostName = "localhost";
        private IEventManager _eventManager;
        private Dictionary<GameCode, GameData> gameDataMap = new();

        public DiscordBotListener(ILogger<DiscordBot> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
            gameDataMap = new Dictionary<GameCode, GameData>();
        }

        [EventListener]
        public void onGameCreated(IGameCreatedEvent e)
        {
            try
            {
                if (e?.Game?.Options is not NormalGameOptions gameOptions)
                {
                    _logger.LogWarning("Game options not found for game creation event.");
                    return;
                }

                int votingTime = (int)gameOptions.VotingTime;
                var gameData = new GameData(e.Game.Code, votingTime);
                gameDataMap.Add(e.Game.Code, gameData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in onGameCreated");
            }
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            try
            {
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                _logger.LogInformation($"Lobby {e.Game.Code} Game is starting.");

                var game = gameDataMap[e.Game.Code];

                if (e.Game.Options is NormalGameOptions gameOptions)
                {
                    int votingTime = (int)gameOptions.VotingTime;

                    foreach (var player in e.Game.Players)
                    {
                        if (player?.Character != null)
                        {
                            game.AddPlayer(player);
                        }
                    }

                    string pattern = "*_match.json";
                    string workingDirectory = Environment.CurrentDirectory;
                    string directoryPath = Path.Combine(workingDirectory, "plugins", "MatchLogs", "Preseason");
                    string[] matchFiles = Directory.GetFiles(directoryPath, pattern);

                    var eventData = new
                    {
                        EventName = "GameStart",
                        MatchID = matchFiles.Length,
                        GameCode = game.GameCode,
                        Players = game.Players.Select(p => p.Character?.PlayerInfo?.PlayerName ?? "Unknown").ToList(),
                        PlayerColors = game.Players.Select(p => p.Character?.PlayerInfo?.CurrentOutfit?.Color ?? 0).ToList(),
                        Impostors = game.Impostors.Where(p => p.Character?.PlayerInfo?.IsImpostor == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                        Crewmates = game.Crewmates.Where(p => p.Character?.PlayerInfo?.IsImpostor != true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                        VotingTime = votingTime
                    };

                    string jsonData = JsonSerializer.Serialize(eventData);
                    SendMessage(jsonData);
                }
                else
                {
                    _logger.LogWarning($"Game options not found for Lobby {e.Game.Code}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnGameStarted for game {e?.Game?.Code}");
            }
        }

        [EventListener]
        public void onMeetingStart(IMeetingStartedEvent e)
        {
            try
            {
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                var game = gameDataMap[e.Game.Code];
                _logger.LogInformation($"Lobby {e.Game.Code} Meeting Started.");

                game.StartMeetingTimer();

                var eventData = new
                {
                    EventName = "MeetingStart",
                    GameCode = game.GameCode,
                    Players = game.Players.Select(p => p.Character?.PlayerInfo?.PlayerName ?? "Unknown").ToList(),
                    DeadPlayers = game.Players.Where(p => p.Character?.PlayerInfo?.IsDead == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                    Impostors = game.Impostors.Where(p => p.Character?.PlayerInfo?.IsImpostor == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                    Crewmates = game.Crewmates.Where(p => p.Character?.PlayerInfo?.IsImpostor != true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList()
                };

                string jsonData = JsonSerializer.Serialize(eventData);
                SendMessage(jsonData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in onMeetingStart for game {e?.Game?.Code}");
            }
        }

        [EventListener]
        public void onMeetingEnd(IMeetingEndedEvent e)
        {
            try
            {
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                var game = gameDataMap[e.Game.Code];
                _logger.LogInformation($"Voting time is set to {game.VotingTime}, {((int)game.GetMeetingTimerElapsed().TotalSeconds)} has elapsed");
                
                bool allPlayersVoted = true;
                
                // Safe null check for MeetingHud and PlayerStates
                if (e.MeetingHud?.PlayerStates != null)
                {
                    foreach (var playerState in e.MeetingHud.PlayerStates)
                    {
                        if (playerState?.IsDead == true) continue;
                        if (playerState?.VoteType == VoteType.Missed)
                        {
                            allPlayersVoted = false;
                            break;
                        }
                    }
                }


                if (!game.IsMeetingTimerRunning || allPlayersVoted)
                {
                    if (allPlayersVoted)
                    {
                        _logger.LogInformation($"Lobby {e.Game.Code} All players voted, sending Meeting Ended.");
                    }
                    else
                    {
                        _logger.LogInformation($"Lobby {e.Game.Code} Meeting Timer finished, sending Meeting Ended.");
                    }

                    var deadPlayers = game.Players
                        .Where(p => p.Character?.PlayerInfo?.IsDead == true)
                        .Select(p => p.Character!.PlayerInfo.PlayerName)
                        .ToList();

                    // Safe null check for exiled player
                    if (e.Exiled?.PlayerInfo?.PlayerName != null && !deadPlayers.Contains(e.Exiled.PlayerInfo.PlayerName))
                    {
                        deadPlayers.Add(e.Exiled.PlayerInfo.PlayerName);
                    }

                    // Safe null check for game players
                    if (e.Game?.Players != null)
                    {
                        foreach (var player in e.Game.Players)
                        {
                            if (player?.Character?.PlayerInfo?.PlayerName == e.Exiled?.PlayerInfo?.PlayerName && !game.DeadPlayers.Contains(player))
                            {
                                game.DeadPlayers.Add(player);
                            }
                        }
                    }

                    var eventData = new
                    {
                        EventName = "MeetingEnd",
                        GameCode = game.GameCode,
                        Players = game.Players.Select(p => p.Character?.PlayerInfo?.PlayerName ?? "Unknown").ToList(),
                        DeadPlayers = deadPlayers,
                        Impostors = game.Impostors.Where(p => p.Character?.PlayerInfo?.IsImpostor == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                        Crewmates = game.Crewmates.Where(p => p.Character?.PlayerInfo?.IsImpostor != true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList()
                    };

                    string jsonData = JsonSerializer.Serialize(eventData);
                    SendMessage(jsonData);

                    game.ResetMeetingTimer();
                }
                else
                {
                    _logger.LogInformation($"Lobby {e.Game.Code} Meeting ended before timer finished, no event sent.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in onMeetingEnd for game {e?.Game?.Code}");
                
                // Send a fallback message even if there's an error
                try
                {
                    if (e?.Game?.Code != null && gameDataMap.ContainsKey(e.Game.Code))
                    {
                        var game = gameDataMap[e.Game.Code];
                        var fallbackData = new
                        {
                            EventName = "MeetingEnd",
                            GameCode = game.GameCode,
                            Players = new List<string>(),
                            DeadPlayers = new List<string>(),
                            Impostors = new List<string>(),
                            Crewmates = new List<string>(),
                            Error = "Data collection failed, using fallback"
                        };
                        SendMessage(JsonSerializer.Serialize(fallbackData));
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to send fallback message");
                }
            }
        }

        [EventListener(EventPriority.Lowest)]
        public void OnGameEnded(IGameEndedEvent e)
        {
            try
            {
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                var game = gameDataMap[e.Game.Code];
                _logger.LogInformation($"Lobby {e.Game.Code} Game has ended.");
                string pattern = "*_match.json";
                string workingDirectory = Environment.CurrentDirectory;
                string directoryPath = Path.Combine(workingDirectory, "plugins", "MatchLogs", "Preseason");
                string[] matchFiles = Directory.GetFiles(directoryPath, pattern);

                var eventData = new
                {
                    EventName = "GameEnd",
                    MatchID = matchFiles.Length,
                    GameCode = game.GameCode,
                    Players = game.Players.Select(p => p.Character?.PlayerInfo?.PlayerName ?? "Unknown").ToList(),
                    PlayerColors = game.Players.Select(p => p.Character?.PlayerInfo?.CurrentOutfit?.Color ?? 0).ToList(),
                    DeadPlayers = game.Players.Where(p => p.Character?.PlayerInfo?.IsDead == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                    Impostors = game.Impostors.Where(p => p.Character?.PlayerInfo?.IsImpostor == true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                    Crewmates = game.Crewmates.Where(p => p.Character?.PlayerInfo?.IsImpostor != true).Select(p => p.Character!.PlayerInfo.PlayerName).ToList(),
                    Result = e.GameOverReason
                };

                string jsonData = JsonSerializer.Serialize(eventData);
                SendMessage(jsonData);
                game.ResetGame();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnGameEnded for game {e?.Game?.Code}");
            }
        }

        [EventListener]
        public void onGameDestroyed(IGameDestroyedEvent e)
        {
            try
            {
                if (!gameDataMap.ContainsKey(e.Game.Code)) return;
                gameDataMap.Remove(e.Game.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in onGameDestroyed for game {e?.Game?.Code}");
            }
        }

        private void SendMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                _logger.LogWarning("Attempted to send empty message");
                return;
            }

            try
            {
                using var client = new TcpClient(hostName, 5000);
                using var stream = client.GetStream();
                
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
                
                _logger.LogDebug($"Message sent successfully: {message}");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, $"Socket error while sending message to {hostName}:5000");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending message");
            }
        }
    }
}