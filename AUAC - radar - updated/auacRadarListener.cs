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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Impostor.Api.Innersloth;

namespace AmongUsRadarAntiCheat
{
    public class auacRadarListener : IEventListener, IDisposable
    {
        private readonly ILogger<auacRadarListener> _logger;
        private readonly IEventManager _eventManager;
        private bool gameStart = false;

        public auacRadarListener(ILogger<auacRadarListener> logger, IEventManager eventManager)
        {
            _logger = logger;
            _eventManager = eventManager;
        }

        private class PlayerVisibility
        {
            public HashSet<int> PlayersInVision { get; set; } = new HashSet<int>();
            public HashSet<int> PlayersOutOfVision { get; set; } = new HashSet<int>();
        }

        private Dictionary<int, PlayerVisibility> playerVisibilityMap = new Dictionary<int, PlayerVisibility>();
        private readonly HashSet<int> specialAreaClients = new HashSet<int>();

        // GameData child tag values (from Among Us protocol)
        private const byte GameDataTag_Data = 0x01;   // Data
        private const byte GameDataTag_Rpc = 0x02;    // RPC
        private const byte GameDataTag_Spawn = 0x04;  // Spawn
        private const byte GameDataTag_Despawn = 0x05;// Despawn
        private const byte GameDataTag_Scene = 0x06;  // SceneChange
        private const byte GameDataTag_Ready = 0x07;  // Ready

        [EventListener]
        public void OnGameStart(IGameStartedEvent e)
        {
            _logger.LogDebug("game started!");
            gameStart = true;
            var gameOptions = e.Game.Options as NormalGameOptions;
            var crewmateVision = gameOptions?.CrewLightMod ?? 0;
            var impostorVision = gameOptions?.ImpostorLightMod ?? 0;
            var map = gameOptions?.Map ?? 0;
            _logger.LogDebug($"{crewmateVision}, {impostorVision}, {map}");
        }
        /*       
               [EventListener]
               public async ValueTask OnEnterVent(IPlayerEnterVentEvent e)
               {
                   if (e?.PlayerControl?.NetworkTransform == null) return;

                   // Stop default broadcast
                   e.IsCancelled = true;

                   var actor = e.PlayerControl;
                   var actorPos = actor.NetworkTransform.Position;

                   // Determine recipients: players who can see the actor OR are in special areas
                   var (playersInVision, playersOutOfVision) = ClassifyPlayers(e.Game.Players, actor, actorPos);
                   var playersInSpecialAreas = GetPlayersInSpecialAreas(e.Game.Players);
                   var recipients = new List<IClientPlayer>(playersInVision);
                   foreach (var p in playersInSpecialAreas)
                   {
                       if (!recipients.Any(x => x.Client.Id == p.Client.Id))
                       {
                           recipients.Add(p);
                       }
                   }

                   foreach (var p in recipients)
                   {
                       if (p?.Client == null) continue;
                       using var writer = e.Game.StartRpc(actor.Physics.NetId, RpcCalls.EnterVent);
                       Rpc19EnterVent.Serialize(writer, e.Vent.Id);
                       await e.Game.FinishRpcAsync(writer, p.Client.Id);
                   }
               }

               [EventListener]
               public async ValueTask OnExitVent(IPlayerExitVentEvent e)
               {
                   if (e?.PlayerControl?.NetworkTransform == null) return;

                   // Stop default broadcast
                   e.IsCancelled = true;

                   var actor = e.PlayerControl;
                   var actorPos = actor.NetworkTransform.Position;

                   // Determine recipients: players who can see the actor OR are in special areas
                   var (playersInVision, playersOutOfVision) = ClassifyPlayers(e.Game.Players, actor, actorPos);
                   var playersInSpecialAreas = GetPlayersInSpecialAreas(e.Game.Players);
                   var recipients = new List<IClientPlayer>(playersInVision);
                   foreach (var p in playersInSpecialAreas)
                   {
                       if (!recipients.Any(x => x.Client.Id == p.Client.Id))
                       {
                           recipients.Add(p);
                       }
                   }

                   foreach (var p in recipients)
                   {
                       if (p?.Client == null) continue;
                       using var writer = e.Game.StartRpc(actor.NetId, RpcCalls.ExitVent);
                       Rpc20ExitVent.Serialize(writer, e.Vent.Id);
                       await e.Game.SendToAsync(writer, p.Client.Id);
                   }
               }
        */


        //     [EventListener]
        // public async ValueTask OnMurderCheck(IPlayerCheckMurderEvent e)
        // {
        //     if (e?.PlayerControl == null || e.Victim == null) return;

        //     // Stop default broadcast
        //     e.IsCancelled = true;
        //     await e.PlayerControl.ForceMurderPlayerAsync(e.Victim, MurderResultFlags.Succeeded);
        //     //e.PlayerControl.
            
        //     var attacker = e.PlayerControl;
        //     var attackerPos = attacker.NetworkTransform.Position;
        //     var victimOwnerId = e.Victim.OwnerId;

        //     // Ensure we see at least one log line regardless of configured minimum level
        //     _logger.LogWarning("OnMurderCheck triggered (attacker={Attacker}, victim={Victim})",
        //         attacker.PlayerInfo?.PlayerName ?? "?",
        //         e.Victim.PlayerInfo?.PlayerName ?? "?");

        //     var (playersInVision, playersOutOfVision) = ClassifyPlayers(e.Game.Players, attacker, attackerPos);
        //     var playersInSpecialAreas = GetPlayersInSpecialAreas(e.Game.Players);

        //     // Group 1: Send to players who are in vision of attacker OR are in special areas, excluding the victim
        //     var visibleRecipients = new List<IClientPlayer>(playersInVision);
        //     foreach (var p in playersInSpecialAreas)
        //     {
        //         if (!visibleRecipients.Any(x => x.Client.Id == p.Client.Id))
        //         {
        //             visibleRecipients.Add(p);
        //         }
        //     }
        //     // Remove the victim from the visible recipients without relying on Client.Id vs OwnerId comparison
        //     visibleRecipients = visibleRecipients
        //         .Where(p => p?.Character == null || p.Character.NetId != e.Victim.NetId)
        //         .ToList();

        //     // Prepare out-of-vision set first
        //     var recipientsOut = new List<IClientPlayer>();
        //     var specialAreaIds = new HashSet<int>(playersInSpecialAreas.Select(x => x.Client.Id));
        //     foreach (var p in playersOutOfVision)
        //     {
        //         if (p?.Client == null) continue;
        //         if (specialAreaIds.Contains(p.Client.Id)) continue; // already in visibleRecipients above
        //         recipientsOut.Add(p);
        //     }

        //     // Ensure victim receives the event
        //     var victimClient = e.Game.Players.FirstOrDefault(x => x.Client.Id == victimOwnerId);
        //     if (victimClient != null && !recipientsOut.Any(x => x.Client.Id == victimOwnerId))
        //     {
        //         recipientsOut.Add(victimClient);
        //     }

        //     // Logging overview
        //     _logger.LogWarning(
        //         "OnMurderCheck: attacker={Attacker} victim={Victim} visible={VisibleCount} outOfVision={OutCount}",
        //         attacker.PlayerInfo?.PlayerName,
        //         e.Victim.PlayerInfo?.PlayerName,
        //         visibleRecipients.Count,
        //         recipientsOut.Count);

        //     _logger.LogWarning("Out-of-vision recipients: {List}",
        //         string.Join(", ", recipientsOut.Select(r => r?.Character?.PlayerInfo?.PlayerName ?? $"Client#{r?.Client?.Id}")));

        //     _logger.LogWarning("Visible recipients: {List}",
        //         string.Join(", ", visibleRecipients.Select(r => r?.Character?.PlayerInfo?.PlayerName ?? $"Client#{r?.Client?.Id}")));

        //     // Handle out-of-vision recipients FIRST (and victim)
        //     foreach (var p in recipientsOut)
        //     {
        //         if (p?.Client == null) continue;
        //         using var writer = e.Game.StartRpc(e.Victim.NetId, RpcCalls.MurderPlayer, p.Client.Id);
        //         Rpc12MurderPlayer.Serialize(writer, e.Victim, MurderResultFlags.Succeeded);
        //         await e.Game.FinishRpcAsync(writer, p.Client.Id);
        //         _logger.LogDebug("Sent MurderPlayer (out-of-vision/victim) to {Name}", p.Character?.PlayerInfo?.PlayerName ?? $"Client#{p.Client.Id}");
        //     }

        //     // Then handle visible recipients
        //     foreach (var p in visibleRecipients)
        //     {
        //         if (p?.Client == null) continue;
        //         using var writer = e.Game.StartRpc(attacker.NetId, RpcCalls.MurderPlayer, p.Client.Id);
        //         Rpc12MurderPlayer.Serialize(writer, e.Victim, MurderResultFlags.Succeeded);
        //         await e.Game.FinishRpcAsync(writer, p.Client.Id);
        //         _logger.LogDebug("Sent MurderPlayer (visible) to {Name}", p.Character?.PlayerInfo?.PlayerName ?? $"Client#{p.Client.Id}");
        //     }
        // }

        [EventListener]
        public async ValueTask OnMovement(IPlayerMovementEvent e) 
        {
            // Null checks to prevent exceptions
            if (e?.PlayerControl?.NetworkTransform == null) return;

            var messageReader = e.PlayerControl.NetworkTransform.PacketMessageReader;

            // Ensure we only cancel and re-route true movement packets
            var isMovementPacket = IsMovementGameData(messageReader, (uint)e.PlayerControl.NetworkTransform.NetId);
            if (!isMovementPacket)
            {
                // Not a movement update; let the server handle it normally
                return;
            }

            // Cancel the default movement behavior only for movement packets
            if (gameStart)
            {
                e.IsCancelled = true;
            }

            var movingPlayer = e.PlayerControl;
            var movingPlayerPosition = movingPlayer.NetworkTransform.Position;

            // Optional structured debug for the current packet
            DebugMovementPacket(messageReader, (uint)movingPlayer.NetworkTransform.NetId);

            // Check transitions into/out of special viewing areas (admin or cams)
            var isNowInSpecial = IsInAdminCoordinates(movingPlayerPosition) || IsInCamsCoordinates(movingPlayerPosition);
            var wasInSpecial = specialAreaClients.Contains(movingPlayer.OwnerId);

            var (playersInVision, playersOutOfVision) = ClassifyPlayers(e.Game.Players, movingPlayer, movingPlayerPosition);

            if (isNowInSpecial && !wasInSpecial)
            {
                specialAreaClients.Add(movingPlayer.OwnerId);
                _logger.LogInformation($"Player {movingPlayer.PlayerInfo?.PlayerName ?? "Unknown"} entered special viewing area (admin/cams)");

                // Immediately show all players to this viewer
                var everyoneElse = e.Game.Players
                    .Where(p => p.Client.Id != movingPlayer.OwnerId && p.Character?.NetworkTransform != null)
                    .ToList();
                await SendPlayerPositionsToMovingPlayer(e.Game, everyoneElse, movingPlayer.OwnerId);
            }
            else if (!isNowInSpecial && wasInSpecial)
            {
                specialAreaClients.Remove(movingPlayer.OwnerId);
                _logger.LogInformation($"Player {movingPlayer.PlayerInfo?.PlayerName ?? "Unknown"} exited special viewing area (admin/cams)");

                // Hide players that are not in normal vision anymore for this viewer
                await SendNullVectorsToMovingPlayer(e.Game, playersOutOfVision, movingPlayer.OwnerId);
            }
            await SendMovementUpdates(e, movingPlayer, playersInVision, playersOutOfVision);
        }


        private bool IsMovementGameData(IMessageReader topLevelReader, uint networkTransformNetId)
        {
            try
            {
                if (topLevelReader == null) return false;

                var originalPos = topLevelReader.Position;

                // Iterate all child messages contained in this GameData payload
                while (topLevelReader.Position < topLevelReader.Length)
                {
                    using var child = topLevelReader.ReadMessage();
                    if (child == null) break;

                    if (child.Tag == GameDataTag_Data)
                    {
                        // Peek the netId of the object this Data message targets
                        var childStart = child.Position;
                        var netId = child.ReadPackedUInt32();

                        if (netId == networkTransformNetId)
                        {
                            // Movement Data targets the player's NetworkTransform object
                            topLevelReader.Seek(originalPos);
                            return true;
                        }

                        // Restore child's position (not strictly required here)
                        child.Seek(childStart);
                    }
                }

                topLevelReader.Seek(originalPos);
                return false;
            }
            catch
            {
                // On any parsing issue, consider it not a movement packet
                return false;
            }
        }

        // Structured debug logs for movement-related GameData
        private void DebugMovementPacket(IMessageReader topLevelReader, uint networkTransformNetId)
        {
            try
            {
                if (topLevelReader == null) return;
                var originalPos = topLevelReader.Position;

                while (topLevelReader.Position < topLevelReader.Length)
                {
                    using var child = topLevelReader.ReadMessage();
                    if (child == null) break;

                    switch (child.Tag)
                    {
                        case GameDataTag_Data:
                        {
                            var start = child.Position;
                            var netId = child.ReadPackedUInt32();
                            var isMovement = netId == networkTransformNetId;

                            // Try to read a bit of the payload for movement packets
                            ushort seq = 0;
                            int positionsCount = 0;
                            try
                            {
                                seq = child.ReadUInt16();
                                positionsCount = child.ReadPackedInt32();
                            }
                            catch { /* Not necessarily a movement payload */ }

                            _logger.LogTrace($"DEBUG: Child=Data NetId={netId} Movement={isMovement} Seq={seq} Positions={positionsCount}");
                            child.Seek(start);
                            break;
                        }
                        case GameDataTag_Rpc:
                            _logger.LogTrace("DEBUG: Child=RPC");
                            break;
                        case GameDataTag_Spawn:
                            _logger.LogTrace("DEBUG: Child=Spawn");
                            break;
                        case GameDataTag_Despawn:
                            _logger.LogTrace("DEBUG: Child=Despawn");
                            break;
                        case GameDataTag_Scene:
                            _logger.LogTrace("DEBUG: Child=SceneChange");
                            break;
                        case GameDataTag_Ready:
                            _logger.LogTrace("DEBUG: Child=Ready");
                            break;
                        default:
                            _logger.LogTrace($"DEBUG: Child=Unknown Tag=0x{child.Tag:X2}");
                            break;
                    }
                }

                topLevelReader.Seek(originalPos);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"DEBUG: Error while debugging movement packet: {ex.Message}");
            }
        }

        public void OnGameEnd(IGameEndedEvent e)
        {
            gameStart = false;
        }

        // Helper function: Classify players into vision categories
        private (List<IClientPlayer>, List<IClientPlayer>) ClassifyPlayers(IEnumerable<IClientPlayer> allPlayers, IInnerPlayerControl movingPlayer, Vector2 movingPlayerPosition)
        {
            var playersInVision = new List<IClientPlayer>();
            var playersOutOfVision = new List<IClientPlayer>();

            foreach (var player in allPlayers)
            {
                // Null checks to prevent exceptions
                if (player?.Client == null || player.Character?.NetworkTransform == null) continue;
                if (player.Client.Id == movingPlayer.OwnerId) continue;
                
                var playerPosition = player.Character.NetworkTransform.Position;
                var distance = CalculateDistance(movingPlayerPosition, playerPosition);
                if (IsInVision(player, movingPlayer, distance)) { playersInVision.Add(player); }
                else { playersOutOfVision.Add(player); }
            }
            return (playersInVision, playersOutOfVision);
        }

        // Helper function: Send movement updates and handle visibility changes
        private async Task SendMovementUpdates(IPlayerMovementEvent e, IInnerPlayerControl movingPlayer, List<IClientPlayer> playersInVision, List<IClientPlayer> playersOutOfVision)
        {
            // Null checks to prevent exceptions
            if (e?.ClientPlayer?.Client == null) return;
            
            int movingPlayerId = e.ClientPlayer.Client.Id;
            var movingPlayerPosition = movingPlayer.NetworkTransform.Position;

            if (!playerVisibilityMap.ContainsKey(movingPlayerId))
            {
                playerVisibilityMap[movingPlayerId] = new PlayerVisibility();
            }

            var visibility = playerVisibilityMap[movingPlayerId];

            // Check if any players are in special areas (admin or cams)
            var playersInSpecialAreas = GetPlayersInSpecialAreas(e.Game.Players);
            
            // Combine normal vision players with players in special areas
            var allPlayersToReceiveUpdates = new List<IClientPlayer>(playersInVision);
            foreach (var specialPlayer in playersInSpecialAreas)
            {
                if (!allPlayersToReceiveUpdates.Any(p => p.Client.Id == specialPlayer.Client.Id))
                {
                    allPlayersToReceiveUpdates.Add(specialPlayer);
                }
            }

            // Send moving player's position to all players in vision AND players in special areas
            await SendMovementPacketToPlayers(e.Game, movingPlayer, allPlayersToReceiveUpdates, movingPlayerId);

            // Send null vector to players who just went out of vision
            var newlyInvisible = playersOutOfVision.Where(p => visibility.PlayersInVision.Contains(p.Client.Id)).ToList();
            await SendNullVectorsToPlayers(e.Game, movingPlayer, newlyInvisible, movingPlayerId, "newly invisible");

            // Send other players' positions to moving player if they just came into vision
            var newlyVisible = playersInVision.Where(p => !visibility.PlayersInVision.Contains(p.Client.Id)).ToList();
            await SendPlayerPositionsToMovingPlayer(e.Game, newlyVisible, movingPlayerId);

            // Send null vectors for players who just went out of vision of the moving player
            var newlyInvisibleToMoving = playersOutOfVision.Where(p => visibility.PlayersInVision.Contains(p.Client.Id)).ToList();
            await SendNullVectorsToMovingPlayer(e.Game, newlyInvisibleToMoving, movingPlayerId);

            // Update the visibility sets
            UpdateVisibilitySets(visibility, playersInVision, playersOutOfVision, movingPlayerId);
        }

        // Helper function: Send movement packet to players in vision
        private async Task SendMovementPacketToPlayers(IGame game, IInnerPlayerControl movingPlayer, List<IClientPlayer> playersInVision, int movingPlayerId)
        {
            if (playersInVision.Count == 0) return;
            
            var movementPacketWriter = MessageWriter.Get(MessageType.Unreliable);
            // Copy the remaining bytes from the current position (matches server broadcast behavior)
            movingPlayer.NetworkTransform.PacketMessageReader.CopyTo(movementPacketWriter);
            foreach (var player in playersInVision)
            {
                if (player?.Client != null && player.Client.Id != movingPlayerId)
                {
                    await game.SendToAsync(movementPacketWriter, player.Client.Id);
                    _logger.LogDebug($"RPC: Sent movement update for player {movingPlayerId} to player {player.Client.Id}");
                }
            }
            movementPacketWriter.Recycle();
        }

        // Helper function: Send null vectors to players who went out of vision
        private async Task SendNullVectorsToPlayers(IGame game, IInnerPlayerControl movingPlayer, List<IClientPlayer> players, int movingPlayerId, string reason)
        {
            foreach (var player in players)
            {
                if (player?.Client != null)
                {
                    await SnapPlayerForTarget(game, Vector2.Zero, movingPlayer, player.Client.Id);
                    _logger.LogDebug($"RPC: Sent null vector for player {movingPlayerId} to player {player.Client.Id} ({reason})");
                }
            }
        }

        // Helper function: Send player positions to moving player
        private async Task SendPlayerPositionsToMovingPlayer(IGame game, List<IClientPlayer> players, int movingPlayerId)
        {
            foreach (var player in players)
            {
                if (player?.Character?.NetworkTransform != null)
                {
                    await SnapPlayerForTarget(game, player.Character.NetworkTransform.Position, player.Character, movingPlayerId);
                    _logger.LogDebug($"RPC: Sent position of player {player.Client.Id} to moving player {movingPlayerId} (newly visible)");
                }
            }
        }

        // Helper function: Send null vectors to moving player for players who went out of vision
        private async Task SendNullVectorsToMovingPlayer(IGame game, List<IClientPlayer> players, int movingPlayerId)
        {
            foreach (var player in players)
            {
                if (player?.Character != null)
                {
                    await SnapPlayerForTarget(game, Vector2.Zero, player.Character, movingPlayerId);
                    _logger.LogDebug($"RPC: Sent null vector for player {player.Client.Id} to moving player {movingPlayerId} (newly invisible to moving)");
                }
            }
        }

        // Helper function: Update visibility sets and log
        private void UpdateVisibilitySets(PlayerVisibility visibility, List<IClientPlayer> playersInVision, List<IClientPlayer> playersOutOfVision, int movingPlayerId)
        {
            visibility.PlayersInVision = new HashSet<int>(playersInVision.Select(p => p.Client.Id));
            visibility.PlayersOutOfVision = new HashSet<int>(playersOutOfVision.Select(p => p.Client.Id));

            _logger.LogDebug($"Updated visibility for player {movingPlayerId}:");
            _logger.LogDebug($"  In vision: {string.Join(", ", visibility.PlayersInVision)}");
            _logger.LogDebug($"  Out of vision: {string.Join(", ", visibility.PlayersOutOfVision)}");
        }

        // Helper function: Check if player is in vision
        private bool IsInVision(IClientPlayer player, IInnerPlayerControl movingPlayer, double distance)
        {
            // Null checks to prevent exceptions
            if (player?.Character?.PlayerInfo == null || movingPlayer?.PlayerInfo == null) return false;
            
            // Check if moving player is in special coordinate boxes
            var movingPlayerPosition = movingPlayer.NetworkTransform.Position;
            if (IsInAdminCoordinates(movingPlayerPosition) || IsInCamsCoordinates(movingPlayerPosition))
            {
                // If player is in special area, they can see all players
                return true;
            }
            
            return player.Character.PlayerInfo.IsDead || movingPlayer.PlayerInfo.IsDead ||
                   (player.Character.PlayerInfo.IsImpostor && distance <= 10.0) ||
                   (!player.Character.PlayerInfo.IsImpostor && distance <= 10.0);
        }

        // Helper function: Check if player is in admin coordinates box
        private bool IsInAdminCoordinates(Vector2 position)
        {
            return position.X >= 22.00f && position.X <= 25.50f &&
                   position.Y >= -22.60f && position.Y <= -20.20f;
        }

        // Helper function: Check if player is in cams coordinates box
        private bool IsInCamsCoordinates(Vector2 position)
        {
            return position.X >= 1.50f && position.X <= 4.30f &&
                   position.Y >= -12.80f && position.Y <= -11.00f;
        }

        // Helper function: Get all players currently in special areas (admin or cams)
        private List<IClientPlayer> GetPlayersInSpecialAreas(IEnumerable<IClientPlayer> allPlayers)
        {
            var playersInSpecialAreas = new List<IClientPlayer>();
            
            foreach (var player in allPlayers)
            {
                if (player?.Character?.NetworkTransform == null) continue;
                
                var playerPosition = player.Character.NetworkTransform.Position;
                if (IsInAdminCoordinates(playerPosition) || IsInCamsCoordinates(playerPosition))
                {
                    playersInSpecialAreas.Add(player);
                }
            }
            
            return playersInSpecialAreas;
        }

        // Helper function: Send SnapTo RPC
        private async Task SnapPlayerForTarget(IGame game, Vector2 position, IInnerPlayerControl snappedPlayer, int targetPlayerId)
        {
            if (snappedPlayer?.NetworkTransform == null) return;
            
            var movementMessage = game.StartRpc(snappedPlayer.NetworkTransform.NetId, RpcCalls.SnapTo, targetPlayerId);
            Rpc21SnapTo.Serialize(movementMessage, position, snappedPlayer.NetworkTransform.IncrementLastSequenceId((ushort)5U));
            await game.FinishRpcAsync(movementMessage, targetPlayerId);
            _logger.LogDebug($"RPC: SnapTo - Player: {snappedPlayer.PlayerInfo?.PlayerName ?? "Unknown"}, Target: {targetPlayerId}, Position: {position}");
        }

        // Helper function: Calculate distance between two points
        private double CalculateDistance(Vector2 vector1, Vector2 vector2)
        {
            double xDiff = vector2.X - vector1.X;
            double yDiff = vector2.Y - vector1.Y;
            return Math.Sqrt((xDiff * xDiff) + (yDiff * yDiff));
        }

        // Helper function: Check if this is a special message (0x10) that should be ignored
        private bool IsSpecialMessage(IMessageReader messageReader)
        {
            try
            {
                if (messageReader == null) return false;
                
                // Save current position
                var originalPosition = messageReader.Position;
                
                // Read target client ID (packed int32)
                var targetClientId = messageReader.ReadPackedInt32();
                
                // Read child message
                var childMessage = messageReader.ReadMessage();
                if (childMessage != null && childMessage.Tag == 0x10)
                {
                    _logger.LogInformation("DEBUG: Detected 0x10 message - allowing normal processing");
                    return true;
                }
                
                // Restore position
                messageReader.Seek(originalPosition);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"DEBUG: Error checking special message: {ex.Message}");
                return false;
            }
        }

        // Debug function: Parse GameDataTo packet to identify InnerNetObject type
        private void DebugPacketMessageReader(IMessageReader messageReader)
        {
            try
            {
                if (messageReader == null)
                {
                    _logger.LogInformation("DEBUG: MessageReader is null");
                    return;
                }

                // Get packet length
                var packetLength = messageReader.Length;
                _logger.LogInformation($"DEBUG: Packet length: {packetLength} bytes");

                // Based on Impostor server code, GameDataTo packets have this structure:
                // 1. Target client ID (packed int32)
                // 2. Child message (GameData message)

                // Read target client ID (packed int32)
                var targetClientId = messageReader.ReadPackedInt32();
                _logger.LogInformation($"DEBUG: Target client ID: {targetClientId}");

                // Read child message
                var childMessage = messageReader.ReadMessage();
                if (childMessage != null)
                {
                    _logger.LogInformation($"DEBUG: Child message tag: 0x{childMessage.Tag:X2}");
                    _logger.LogInformation($"DEBUG: Child message length: {childMessage.Length} bytes");

                    // Parse child message based on tag
                    switch (childMessage.Tag)
                    {
                        case 0x01: // Data
                            _logger.LogInformation("DEBUG: Child message type: Data");
                            ParseDataMessage(childMessage);
                            break;
                        case 0x02: // RPC
                            _logger.LogInformation("DEBUG: Child message type: RPC");
                            ParseRpcMessage(childMessage);
                            break;
                        case 0x04: // Spawn
                            _logger.LogInformation("DEBUG: Child message type: Spawn");
                            break;
                        case 0x05: // Despawn
                            _logger.LogInformation("DEBUG: Child message type: Despawn");
                            break;
                        case 0x06: // SceneChange
                            _logger.LogInformation("DEBUG: Child message type: SceneChange");
                            break;
                        case 0x07: // Ready
                            _logger.LogInformation("DEBUG: Child message type: Ready");
                            break;
                        case 0x08: // ChangeSettings
                            _logger.LogInformation("DEBUG: Child message type: ChangeSettings");
                            break;
                        case 0x10: // Special message
                            _logger.LogInformation("DEBUG: Child message type: Special (0x10)");
                            break;
                        default:
                            _logger.LogInformation($"DEBUG: Child message type: Unknown (0x{childMessage.Tag:X2})");
                            break;
                    }
                }

                // Log remaining data in main packet
                var remainingBytes = packetLength - messageReader.Position;
                _logger.LogInformation($"DEBUG: Remaining main packet bytes: {remainingBytes}");

            }
            catch (Exception ex)
            {
                _logger.LogInformation($"DEBUG: Error parsing packet: {ex.Message}");
            }
        }

        // Helper function: Parse Data message (0x01)
        private void ParseDataMessage(IMessageReader childMessage)
        {
            try
            {
                // Read InnerNetObject ID (packed int32)
                var innerNetObjectId = childMessage.ReadPackedInt32();
                _logger.LogInformation($"DEBUG: InnerNetObject ID: {innerNetObjectId}");

                // Identify InnerNetObject type based on ID
                string objectType = GetInnerNetObjectType(innerNetObjectId);
                _logger.LogInformation($"DEBUG: InnerNetObject type: {objectType}");

                // Log remaining data in child message
                var remainingChildBytes = childMessage.Length - childMessage.Position;
                _logger.LogInformation($"DEBUG: Remaining child message bytes: {remainingChildBytes}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"DEBUG: Error parsing Data message: {ex.Message}");
            }
        }

        // Helper function: Parse RPC message (0x02)
        private void ParseRpcMessage(IMessageReader childMessage)
        {
            try
            {
                // Read InnerNetObject ID (packed int32)
                var innerNetObjectId = childMessage.ReadPackedInt32();
                _logger.LogInformation($"DEBUG: InnerNetObject ID: {innerNetObjectId}");

                // Read RPC call ID (packed int32)
                var rpcCallId = childMessage.ReadPackedInt32();
                _logger.LogInformation($"DEBUG: RPC call ID: {rpcCallId}");

                // Identify InnerNetObject type based on ID
                string objectType = GetInnerNetObjectType(innerNetObjectId);
                _logger.LogInformation($"DEBUG: InnerNetObject type: {objectType}");

                // Log remaining data in child message
                var remainingChildBytes = childMessage.Length - childMessage.Position;
                _logger.LogInformation($"DEBUG: Remaining child message bytes: {remainingChildBytes}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"DEBUG: Error parsing RPC message: {ex.Message}");
            }
        }

        // Helper function: Identify InnerNetObject type based on ID
        private string GetInnerNetObjectType(int objectId)
        {
            // Based on Among Us protocol documentation
            // These are typical ID ranges for different object types
            if (objectId >= 1 && objectId <= 10)
                return "SkeldShipStatus";
            else if (objectId >= 11 && objectId <= 20)
                return "MeetingHud";
            else if (objectId >= 21 && objectId <= 30)
                return "LobbyBehaviour";
            else if (objectId >= 31 && objectId <= 40)
                return "GameData";
            else if (objectId >= 41 && objectId <= 50)
                return "PlayerControl";
            else if (objectId >= 51 && objectId <= 60)
                return "MiraShipStatus";
            else if (objectId >= 61 && objectId <= 70)
                return "PolusShipStatus";
            else if (objectId >= 71 && objectId <= 80)
                return "DleksShipStatus";
            else if (objectId >= 81 && objectId <= 90)
                return "VoteBanSystem";
            else if (objectId >= 91 && objectId <= 100)
                return "PlayerPhysics";
            else if (objectId >= 101 && objectId <= 110)
                return "CustomNetworkTransform";
            else
                return "Unknown";
        }

        public void Dispose()
        {
            // Cleanup any resources if needed
            playerVisibilityMap?.Clear();
        }
    }
}