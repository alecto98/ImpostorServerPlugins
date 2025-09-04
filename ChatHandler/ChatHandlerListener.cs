using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Microsoft.Extensions.Logging;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Innersloth;
using System.Text;

namespace ChatHandlerPlugin
{
    public class ChatHandlerListener : IEventListener
    {
        private readonly ILogger<ChatHandlerPlugin> _logger;
        private readonly Config _config;
        private Dictionary<GameCode, GameData> gameDataMap = new Dictionary<GameCode, GameData>();
        private List<IInnerPlayerControl> list_announced = new List<IInnerPlayerControl>();

        public ChatHandlerListener(ILogger<ChatHandlerPlugin> logger, Config config)
        {
            _logger = logger;
            _config = config;
        }

        #region Game Events

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            var gameData = new GameData();
            foreach (var player in e.Game.Players)
            {
                if (player.Character?.PlayerInfo != null)
                {
                    gameData.AddPlayer(player, player.Character.PlayerInfo.IsImpostor);
                }
            }
            gameDataMap.Add(e.Game.Code, gameData);
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            gameDataMap[e.Game.Code].ResetGame();
            gameDataMap.Remove(e.Game.Code);
            
        }

        #endregion

        #region Player Events

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            _logger.LogInformation("Player {player} > spawned", e.PlayerControl.PlayerInfo.PlayerName);

            var clientPlayer = e.ClientPlayer;
            var playerControl = e.PlayerControl;

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                if (!list_announced.Contains(playerControl))
                {
                    await playerControl.SendChatToPlayerAsync(_config.AnnouncementMessage);
                    list_announced.Add(playerControl);
                }
            });
        }

        [EventListener]
        public void OnPlayerDestroyed(IPlayerDestroyedEvent e)
        {
            list_announced.Remove(e.PlayerControl);
        }

        [EventListener(EventPriority.Lowest)]
        public async void OnPlayerChat(IPlayerChatEvent e )
        {
            string input = e.Message.Trim();
            if (input.Length < 1) return;

            if (!input.StartsWith("/") && !input.StartsWith("?"))
            {
                return; // Allow normal chat messages
            }

            string command = input.Split(' ')[0].ToLower();

            if (_config.ImpostorChatCommands.Contains(command) || input.StartsWith("//") || input.StartsWith("??"))
            {
                e.IsCancelled = true;
                HandleImpostorChat(e);
                return;
            }

            await HandleCommand(e, command);
        }

        #endregion

        #region Command Handlers

        private async Task HandleCommand(IPlayerChatEvent e, string command)
        {
            e.IsCancelled = true;
            switch (command)
            {
                case "/task":
                case "?task":
                case "/tasks":
                case "?tasks":
                    await HandleTasksCommand(e);
                    break;
                case "/help":
                case "?help":
                    await e.PlayerControl.SendChatToPlayerAsync(HandleHelpCommand(e.Message));
                    break;
                case "/rules":
                case "?rules":
                    await e.PlayerControl.SendChatToPlayerAsync(_config.rulesMessage);
                    break;
                case "/timer":
                case "?timer":
                    await e.PlayerControl.SendChatToPlayerAsync(_config.timerMessage);
                    break;
                case "/skin":
                    await HandleSkinCommand(e);
                    break;
                case "/cancel":
                case "?cancel":
                    break;
                case "/end":
                case "?end":
                    break;
                default:
                    await e.PlayerControl.SendChatToPlayerAsync(_config.wrongCommandMessage);
                    break;
            }
        }

        private void HandleImpostorChat(IPlayerChatEvent e)
        {
            if (e.Game.GameState == GameStates.NotStarted) return;
            if (e.ClientPlayer?.Character?.PlayerInfo == null) return;

            var impostors = gameDataMap[e.Game.Code].Impostors;
            var players = gameDataMap[e.Game.Code].Players;

            if (!impostors.Contains(e.ClientPlayer))
            {
                e.ClientPlayer.Character.SendChatToPlayerAsync("Crewmates can't use Impostor Chat", e.ClientPlayer.Character);
                return;
            }

            string message = StripPrefix(e.Message).Trim();
            string senderName = e.ClientPlayer.Character.PlayerInfo.PlayerName;
            string formattedMessage = $"Imp Chat: {message}";

            SendImpostorMessage(players, e.ClientPlayer, formattedMessage);
        }

        private string HandleHelpCommand(string fullMessage)
        {
            string[] parts = fullMessage.Split(' ', 2);
            if (parts.Length > 1)
            {
                switch (parts[1].Trim().ToLower())
                {
                    case "timer":
                        return "Timer Command:\n" + _config.timerMessage;
                    case "rules":
                        return "Rules Command:\n" + _config.rulesMessage;
                    case "cancel":
                        return "Cancel Command:\n" + _config.cancelMessage;
                    case "tasks":
                        return "Tasks Command:\n" + _config.tasksMessage;
                    case "impostor":
                    case "impostorchat":
                        return "Impostor Chat:\n" + _config.impChatMessage;
                    default:
                        return "Unknown command. Type '/help' for a list of available commands.";
                }
            }
            return _config.helpMessage;
        }

        private async Task HandleTasksCommand(IPlayerChatEvent e)
        {
            if (e.ClientPlayer?.Character?.PlayerInfo == null)
            {
                _logger.LogWarning("HandleTasksCommand called with null player info");
                return;
            }

            if (!e.ClientPlayer.Character.PlayerInfo.IsDead)
            {
                await e.ClientPlayer.Character.SendChatToPlayerAsync(_config.tasksMessage);
                return;
            }

            var taskSummary = GenerateTaskSummary(e.Game);
            await e.ClientPlayer.Character.SendChatToPlayerAsync(taskSummary);
        }

        private async Task HandleSkinCommand(IPlayerChatEvent e)
        {
            try
            {
                await e.PlayerControl.SetSkinAsync("skin_witch");
                await e.PlayerControl.SendChatToPlayerAsync("Skin changed to Witch!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting skin for player {player}", e.PlayerControl.PlayerInfo.PlayerName);
                await e.PlayerControl.SendChatToPlayerAsync("Failed to change skin. Please try again.");
            }
        }

        #endregion

        #region Helper Methods

        private string StripPrefix(string message)
        {
            if (message.StartsWith("//") || message.StartsWith("??"))
            {
                return message.Substring(2);
            }
            else if (message.StartsWith("/say") || message.StartsWith("?say"))
            {
                return message.Substring(message.StartsWith("/say") ? 4 : 5);
            }
            else if (message.StartsWith("/w") || message.StartsWith("?w"))
            {
                return message.Substring(2);
            }
            return message;
        }

        private void SendImpostorMessage(List<IClientPlayer> players, IClientPlayer sender, string formattedMessage)
        {
            foreach (var player in players)
            {
                if (player == null || player == sender) continue;

                if (player.Character?.PlayerInfo != null && (player.Character.PlayerInfo.IsDead || player.Character.PlayerInfo.IsImpostor))
                {
                    string messageToSend = "<#c51111>" + formattedMessage;
                    player.Character.SendChatToPlayerAsync(messageToSend, player.Character);
                }
            }
        }

        private string GenerateTaskSummary(IGame game)
        {
            var taskSummary = new StringBuilder("Tasks remaining:\n");

            foreach (var player in game.Players)
            {
                if (player?.Character?.PlayerInfo == null || player.Character.PlayerInfo.IsImpostor)
                    continue;

                int tasksRemaining = player.Character.PlayerInfo.Tasks?.Count(task => task != null && !task.Complete) ?? 0;
                taskSummary.AppendLine($"Player {player.Character.PlayerId}: {tasksRemaining} has task(s) left");
            }

            return taskSummary.ToString();
        }

        #endregion
    }
}