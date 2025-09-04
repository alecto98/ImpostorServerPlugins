using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameTimerPlugin
{
    internal class TimerPlugin : IEventListener
    {
        private readonly ILogger<GameTimerPlugin> _logger;
        private TimeSpan duration;
        private Dictionary<GameCode, GameData> gameDataMap = new Dictionary<GameCode, GameData>();
        private string emptyString = "";
        private Dictionary<GameCode, TimeSpan> semaphore = new Dictionary<GameCode, TimeSpan>();

        public TimerPlugin(ILogger<GameTimerPlugin> logger)
        {
            _logger = logger;
            duration = TimeSpan.FromMinutes(17);
        }

        [EventListener]
        public void OnGameStart(IGameStartedEvent e)
        {
            var gameData = new GameData();
            var gameTime = duration;
            foreach (var player in e.Game.Players)
            {
                var isImpostor = player.Character?.PlayerInfo?.IsImpostor ?? false;
                gameData.AddPlayer(player, isImpostor);
            }
            gameData.TimerElapsed += (gameDataInstance) =>
            {
                if (!gameDataInstance.IsInMeeting)
                {
                    skillIssue(gameDataInstance);
                }
                else
                {
                    gameDataInstance.TimerIsUp = true;
                    var anyPlayer = gameDataInstance.Players.FirstOrDefault(p => p?.Character != null);
                    anyPlayer?.Character?.SendChatAsync("Match Timer is up, Impostors have Won!");
                }
            };
            gameData.StartTimer(gameTime);
            gameDataMap.Add(e.Game.Code, gameData);
        }

        [EventListener]
        public void onGameEnd(IGameEndedEvent e)
        {
            var endingCode = e.Game.Code;
            if (gameDataMap.ContainsKey(endingCode))
            {
                gameDataMap[endingCode].ResetGame();

                // Remove the GameData object associated with this game code
                gameDataMap.Remove(endingCode);
            }
        }

        [EventListener]
        public void playerMurdered(IPlayerMurderEvent e)
        {
            var playerKilled = e.Victim;
            var currentGame = e.Game.Code;

            var killedClient = e.Game.Players.FirstOrDefault(p => p.Character == playerKilled);

            if (killedClient != null && gameDataMap.ContainsKey(currentGame))
            {
                gameDataMap[currentGame].MarkAsDead(killedClient);
            }
        }

        [EventListener]
        public void onMeetingStarted(IMeetingStartedEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            GameData gameData = gameDataMap[e.Game.Code];
            gameData.MeetingStarted();
            var combinedList = new List<IClientPlayer>(gameData.Crewmates.Concat(gameData.Impostors));
            if (combinedList.Count == 0) { return; }
            Shuffle(combinedList);
            var random = new Random();
            var randomIndex = random.Next(combinedList.Count);
            TimeSpan remainingTime = GetRemainingTime(gameData);
            var target = combinedList[randomIndex]?.Character;
            target?.SendChatAsync($"There are {remainingTime.Minutes} minute{(remainingTime.Minutes == 1 ? emptyString : "s")} and {remainingTime.Seconds} second{(remainingTime.Seconds == 1 ? emptyString : "s")} left.");
        }

        [EventListener]
        public void onMeetingEnded(IMeetingEndedEvent e)
        {
            if (!gameDataMap.ContainsKey(e.Game.Code)) return;
            gameDataMap[e.Game.Code].MeetingEnded();
            var gameData = gameDataMap[e.Game.Code];
            if (!e.IsTie)
            {
                if (e.Exiled == null) 
                { 
                    _logger.LogInformation("GameTimerPlugin: Skip Majority."); 
                }
                else
                {
                    var voted = e.Exiled.PlayerId;
                    foreach (var player in gameData.Players)
                    {
                        if (player?.Character != null && player.Character.PlayerId == voted)
                        {
                            gameData.MarkAsDead(player);
                        }
                    }
                }
            }
            // Check Timer
            if (gameData.IsTimerUp())
            {
                if (gameData.Impostors.Count != 0)
                {
                    Task.Run(() => genocide(gameData));
                }
            }
        }

        [EventListener]
        public void onDisconnection(IGamePlayerLeftEvent e)
        {
            if (gameDataMap.ContainsKey(e.Game.Code))
            {
                gameDataMap[e.Game.Code].RemovePlayer(e.Player);
            }
        }

        [EventListener]
        public void onPlayerDestroy(IPlayerDestroyedEvent e) 
        {
            if(gameDataMap.ContainsKey(e.Game.Code))
            {
                if (gameDataMap[e.Game.Code].Players.Contains(e.ClientPlayer))
                {
                    gameDataMap[e.Game.Code].RemovePlayer(e.ClientPlayer);
                }
            }
        }

        private async Task genocide(GameData gameData)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            skillIssue(gameData);
        }

        [EventListener]
        public void onPlayerChat(IPlayerChatEvent e)
        {
            //_logger.LogInformation(e.Message);
            //string prefix = "?";
            string commandTimer = "/timer";
            string altTimer = "?timer";
            string execute = "/skillissue_dev";
            //_logger.LogInformation((!e.Message.StartsWith(prefix)).ToString());
            //_logger.LogInformation((!e.Message.Equals(commandTimer)).ToString());
            //_logger.LogInformation((!e.Message.Equals(execute)).ToString());

            //if (!e.Message.StartsWith(prefix)) return;
            if (!(e.Message.ToLower().Equals(commandTimer) || e.Message.ToLower().Equals(execute) || e.Message.ToLower().Equals(altTimer))) return;
            e.IsCancelled = true;
            if (e.Message.ToLower().Equals(commandTimer) || e.Message.ToLower().Equals(altTimer))
            {
                if (gameDataMap.ContainsKey(e.Game.Code))
                {
                    TimeSpan remainingTime = GetRemainingTime(gameDataMap[e.Game.Code]);
                    var recipient = e.ClientPlayer.Character;
                    recipient?.SendChatToPlayerAsync($"There are {remainingTime.Minutes} minute{(remainingTime.Minutes == 1 ? emptyString : "s")} and {remainingTime.Seconds} second{(remainingTime.Seconds == 1 ? emptyString : "s")} left.", recipient);
                }
                else
                {
                    var recipient = e.ClientPlayer.Character;
                    recipient?.SendChatToPlayerAsync($"Game has not started yet.", recipient);
                }
            }
            if (e.Message.Equals(execute) && e.ClientPlayer.IsHost) {
                _logger.LogInformation("Yup Execute");
                var gameData = gameDataMap[e.Game.Code];
                skillIssue(gameData);
                //_commandHandler.onTimerCommand()
            }
            else
            {
                return;
            }

        }

        private TimeSpan GetRemainingTime(GameData gameData)
        {
            DateTime currentTime = DateTime.UtcNow;
            TimeSpan elapsedTime = currentTime - gameData.StartTime;
            TimeSpan remainingTime = gameData.TimerDuration - elapsedTime;

            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }


        private async void skillIssue(GameData gameData)
        {
            _logger.LogInformation("GameTimerPlugin: MURDER TIME!");

            _logger.LogInformation($"number of imps: {gameData.Impostors.Count}");
            _logger.LogInformation($"number of crew: {gameData.Crewmates.Count}");
            
            var impostors = gameData.Impostors;
            var killer = impostors.FirstOrDefault(p => p?.Character != null && !p.Character.PlayerInfo.IsDead);
            if (killer == null)
            {
                _logger.LogInformation("GameTimerPlugin: No alive impostor found to perform kills.");
                return;
            }

            foreach (var player in gameData.Players)
            {
                if (player?.Character == null) continue;
                if (player.Character.PlayerInfo.IsDead) continue;
                if (impostors.Contains(player)) continue;

                await killer.Character!.MurderPlayerAsync(player.Character);
                await Task.Delay(100);
            }
        }

        public void Shuffle<T>(List<T> list)
        {
            var random = new Random();
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
