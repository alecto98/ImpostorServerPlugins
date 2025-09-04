using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Custom;
using Microsoft.Extensions.Logging;
using Impostor.Hazel;

using Impostor.Hazel.Abstractions;
using Impostor.Api.Innersloth.GameOptions;
using System.Numerics;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Api.Net.Inner;
using Impostor.Api.Net.Inner.Objects.Components;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Innersloth;
using System.Data;
using Impostor.Api.Events.Meeting;

namespace AmongUsRolesAntiCheat
{
    public class auacRolesListener : IEventListener
    {
        private readonly ILogger<auacRolesListener> logger;
        private readonly IEventManager eventManager;
        private bool gameStart = false;
        public auacRolesListener(ILogger<auacRolesListener> logger, IEventManager eventManager)
        {
            this.logger = logger;
            this.eventManager = eventManager;
        }


        [EventListener]
        public void OnSetRoles(IPlayerSetRoleEvent e)
        {
            if (!gameStart)
            {
                e.IsCancelled = true;
            }
        }

        [EventListener]
        public async ValueTask OnGameStart(IGameStartedEvent e)
        {
            gameStart = true;
            logger.LogDebug("Game has started! Assigning roles");
            await AssignRoles(e.Game);
            await SyncData(e.Game);
            gameStart = false;
        }

        [EventListener] //players dont need to know others finished tasks
        public async ValueTask OnTaskFinished(IPlayerCompletedTaskEvent e)
        {
            e.IsCancelled = true;
            logger.LogDebug("Task finished, making it cancelled");
            using var writer = e.Game.StartRpc(e.PlayerControl.NetId, RpcCalls.CompleteTask);
            Rpc01CompleteTask.Serialize(writer, e.Task.Id);
            await e.Game.FinishRpcAsync(writer, e.Game.HostId);
        }

        [EventListener]
        public async ValueTask OnDeath(IPlayerMurderEvent e)
        {
            logger.LogDebug($"{e.Victim.PlayerInfo.PlayerName} has died. Updating their view of impostor roles.");

            var game = e.Game;
            if (await HandleGameEnd(game))
            {
                logger.LogDebug("Game has ended. Skipping further processing.");
                return;
            }
            if (e.Victim != null)
            {
                await SyncRolesForPlayer(game, e.Victim);
            }
        }
        
        [EventListener]
        public async ValueTask OnMeetingStart(IMeetingStartedEvent e)
        {
            logger.LogDebug("Meeting has started. Resetting roles for alive crewmates.");

            var game = e.Game;
            var players = game.Players.ToList();
            var aliveCrewmates = players.Where(p => p.Character != null && !p.Character.PlayerInfo.IsDead && !p.Character.PlayerInfo.IsImpostor && p != game.Host).ToList();

            foreach (var crewmate in aliveCrewmates)
            {
                if (crewmate.Character == null) continue;

                // Set all alive players to appear as crewmates for this crewmate
                foreach (var player in players)
                {
                    if (player.Character == null || player.Character.PlayerInfo.IsDead) continue;

                    var apparentRole = player.Character.PlayerInfo.IsDead? RoleTypes.CrewmateGhost : RoleTypes.Crewmate;

                    await SetRoleForAsync(game, player.Character, apparentRole, crewmate.Character, false);
                }

                logger.LogDebug($"Reset roles for {crewmate.Character.PlayerInfo.PlayerName}");
            }

            logger.LogDebug("All roles have been reset for alive crewmates at the start of the meeting.");
        }
        
        [EventListener]
        public async ValueTask OnMeetingEnd(IMeetingEndedEvent e)
        {
            var game = e.Game;
            if (await HandleGameEnd(game))
            {
                logger.LogDebug("Game has ended. Skipping further processing.");
                return;
            }

            var players = game.Players.ToList();
            var alivePlayers = players.Where(p => p.Character != null && 
                                          !p.Character.PlayerInfo.IsDead && 
                                          p.Character.PlayerInfo.NetId != e.Exiled?.PlayerInfo.NetId).ToList();
            var aliveCrewmates = alivePlayers.Where(p => !p.Character.PlayerInfo.IsImpostor && p != game.Host).ToList();

            if (alivePlayers.Count <= aliveCrewmates.Count)
            {
                logger.LogDebug("Not enough players to set a fake impostor. Skipping.");
                return;
            }

            var random = new Random();
            foreach (var crewmate in aliveCrewmates)
            {
                if (crewmate.Character == null) continue;

                var potentialImpostors = alivePlayers.Where(p => p != crewmate).ToList();
                if (potentialImpostors.Count == 0)
                {
                    logger.LogDebug($"No potential impostors for {crewmate.Character.PlayerInfo.PlayerName}. Skipping.");
                    continue;
                }

                var fakeImpostor = potentialImpostors[random.Next(potentialImpostors.Count)];
                if (fakeImpostor.Character == null) continue;

                // Set the fake impostor for this crewmate
                await SetRoleForAsync(game, fakeImpostor.Character, RoleTypes.Impostor, crewmate.Character, false);
                logger.LogDebug($"Set {fakeImpostor.Character.PlayerInfo.PlayerName} as fake Impostor for {crewmate.Character.PlayerInfo.PlayerName}");
            }

            if (e.Exiled != null) // Sync roles for the exiled player
            {
                await SyncRolesForPlayer(e.Game, e.Exiled);
                logger.LogDebug($"{e.Exiled.PlayerInfo.PlayerName} has died. Updating their view of impostor roles.");
            }
        }

        [EventListener]
        public async ValueTask OnChat(IPlayerChatEvent e)
        {
            if (e.Message is "/haunt" or "?haunt" && e.PlayerControl.PlayerInfo.IsDead)
            {
                await SyncRolesForPlayer(e.Game, e.PlayerControl);
            }
        }


        private async ValueTask<bool> HandleGameEnd(IGame e)
        {
            var players = e.Players.ToList();
            var alivePlayers = players.Where(p => p.Character != null && !p.Character.PlayerInfo.IsDead).ToList();
            var aliveImpostors = alivePlayers.Count(p => p.Character.PlayerInfo.IsImpostor);
            var aliveCrewmates = alivePlayers.Count - aliveImpostors;

            if (aliveImpostors == aliveCrewmates || aliveImpostors == 0)
            {
                logger.LogDebug("Game ending condition met. Revealing all roles.");
                foreach (var player in players)
                {
                    if (player.Character == null || player == e.Host) continue;
                    await SyncRolesForPlayer(e, player.Character);
                }
                return true;
            }
            return false;
        }


        private async ValueTask AssignRoles(IGame game)
        {
            foreach (var player in game.Players)
            {
                if (player?.Character?.PlayerInfo?.RoleType == null)
                {
                    continue;
                }

                _ = SetRoleForAsync(game, player.Character, (RoleTypes)player.Character.PlayerInfo.RoleType, player.Character, true);
                _ = SetRoleForDesync(game, player.Character, RoleTypes.Crewmate, new[] { player.Character, game.Host?.Character }, true);
            }

            await Task.Delay(200);
        }

        private async ValueTask SyncData(IGame game)
        {
            foreach (var player in game.Players)
            {
                if (player?.Character != null && game.Host != null)
                {
                    var writer = game.StartGameData();
                    writer.StartMessage(1);
                    writer.WritePacked((uint)player.Character.PlayerInfo.NetId);
                    await player.Character.PlayerInfo.SerializeAsync(writer, false);
                    writer.EndMessage();
                    writer.EndMessage();
                    await game.SendToAllExceptAsync(writer, game.Host.Client.Id); 
                }
            }
        }

        private async ValueTask SetRoleForAsync(IGame game, IInnerPlayerControl player, RoleTypes role, IInnerPlayerControl? target = null, bool isIntro = false)
        {
            if (game.Host?.Character == target)
            {
                return;
            }

            if (target == null)
            {
                target = player;
            }

            var writer = game.StartGameData();

            if (isIntro)
            {
                player.PlayerInfo.Disconnected = true;
                writer.StartMessage(1);
                writer.WritePacked((uint)player.PlayerInfo.NetId);
                await player.PlayerInfo.SerializeAsync(writer, false);
                writer.EndMessage();
            }

            writer.StartMessage(2); // RPC call
            writer.WritePacked((uint)player.NetId);
            writer.Write((byte)RpcCalls.SetRole);

            Rpc44SetRole.Serialize(writer, role, true);
            writer.EndMessage();

            await game.FinishGameDataAsync(writer, game.Players.First(p => p.Character == target).Client.Id);

            if (isIntro)
            {
                player.PlayerInfo.Disconnected = false;
            }

            logger.LogDebug($"Set {player.PlayerInfo.PlayerName} Role to {role} for {target.PlayerInfo.PlayerName}");
        }

        private async ValueTask SetRoleForDesync(IGame game, IInnerPlayerControl player, RoleTypes role, IInnerPlayerControl?[] targets, bool isIntro = false)
        {
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    targets[i] = player;
                }
            }

            foreach (var pc in game.Players.Select(p => p.Character))
            {
                if (pc == null || targets.Contains(pc))
                {
                    continue;
                }

                var writer = game.StartGameData();

                if (isIntro)
                {
                    player.PlayerInfo.Disconnected = true;
                    writer.StartMessage(1);
                    writer.WritePacked((uint)player.PlayerInfo.NetId);
                    await player.PlayerInfo.SerializeAsync(writer, false);
                    writer.EndMessage();
                }

                writer.StartMessage(2); // RPC call
                writer.WritePacked((uint)player.NetId);
                writer.Write((byte)RpcCalls.SetRole);

                var newRole = pc.PlayerInfo.IsImpostor && player.PlayerInfo.IsImpostor
                    ? (player.PlayerInfo.IsDead ? RoleTypes.ImpostorGhost : RoleTypes.Impostor)
                    : role;
                Rpc44SetRole.Serialize(writer, newRole, true);
                writer.EndMessage();

                await game.FinishGameDataAsync(writer, game.Players.First(p => p.Character == pc).Client.Id);

                if (isIntro)
                {
                    player.PlayerInfo.Disconnected = false;
                }

                logger.LogDebug($"Desync {player.PlayerInfo.PlayerName} Role to {role} for {pc.PlayerInfo.PlayerName}");
            }
        }
        
        private async ValueTask SyncRolesForPlayer(IGame game, IInnerPlayerControl player)
        {
            logger.LogDebug($"Syncing roles for player {player.PlayerInfo.PlayerName}");

            var players = game.Players.ToList();
            // Set roles for all players
            foreach (var otherPlayer in players)
            {
                if (otherPlayer.Character == null || otherPlayer.Character == player) continue;

                var role = otherPlayer.Character.PlayerInfo.IsImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate;
                if (otherPlayer.Character.PlayerInfo.IsDead)
                {
                    role = otherPlayer.Character.PlayerInfo.IsImpostor ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost;
                }

                await SetRoleForAsync(game, otherPlayer.Character, role, player, false);
            }

            // Set the player's own role
            var playerRole = player.PlayerInfo.IsImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate;
            if (player.PlayerInfo.IsDead)
            {
                playerRole = player.PlayerInfo.IsImpostor ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost;
            }
            await SetRoleForAsync(game, player, playerRole, player, false);

            logger.LogDebug($"Finished syncing roles for player {player.PlayerInfo.PlayerName}");
        }
    }
}
