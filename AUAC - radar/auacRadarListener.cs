using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Managers;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Customization;
using Microsoft.Extensions.Logging;
using Impostor.Api.Net.Inner;

namespace AmongUsRadarAntiCheat
{
    public class auacRadarListener : IEventListener, IDisposable
    {
        private readonly ILogger<auacRadarListener> _logger;
        private readonly Dictionary<IGame, Dictionary<int, PlayerCosmetics>> originalCosmeticsByGame = new();

        // Config
        private readonly bool allowImpostorsToSeeEveryone;

        public auacRadarListener(ILogger<auacRadarListener> logger, bool allowImpostorFullVision = true)
        {
            _logger = logger;
            allowImpostorsToSeeEveryone = allowImpostorFullVision;
        }

        private sealed class PlayerCosmetics
        {
            public string Name { get; set; } = string.Empty;
            public ColorType Color { get; set; }
            public string HatId { get; set; } = string.Empty;
            public string SkinId { get; set; } = string.Empty;
            public string VisorId { get; set; } = string.Empty;
            public string NamePlateId { get; set; } = string.Empty;
        }

        // Core RPC helpers
        private async Task MakePlayerUnknownAsync(IInnerPlayerControl target, IEnumerable<IClientPlayer> receivers)
        {
            if (target?.PlayerInfo == null) return;
            var game = receivers.FirstOrDefault()?.Game ?? target.Game;
            if (game == null) return;

            // snapshot
            var gameCosmetics = GetOrCreateGameCosmetics(game);
            if (!gameCosmetics.ContainsKey(target.OwnerId))
            {
                gameCosmetics[target.OwnerId] = SnapshotPlayerCosmetics(target);
            }

            foreach (var receiver in receivers)
            {
                if (receiver?.Client == null || receiver.Character == null) continue;
                if (receiver.Client.Id == target.OwnerId) continue;

                // If impostor-vision is enabled AND receiver is an impostor, skip hiding
                if (allowImpostorsToSeeEveryone && receiver.Character.PlayerInfo?.IsImpostor == true)
                    continue;

                var clientId = receiver.Client.Id;

                using (var w1 = game.StartRpc(target.NetId, RpcCalls.SetName, clientId))
                {
                    Rpc06SetName.Serialize(w1, target.NetId, "Unknown");
                    await game.FinishRpcAsync(w1, clientId);
                }
                using (var w2 = game.StartRpc(target.NetId, RpcCalls.SetColor, clientId))
                {
                    Rpc08SetColor.Serialize(w2, target.NetId, (ColorType)20);
                    await game.FinishRpcAsync(w2, clientId);
                }
                using (var w3 = game.StartRpc(target.NetId, RpcCalls.SetHatStr, clientId))
                {
                    Rpc39SetHatStr.Serialize(w3, "hat_NoHat", target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetHatStr));
                    await game.FinishRpcAsync(w3, clientId);
                }
                using (var w4 = game.StartRpc(target.NetId, RpcCalls.SetSkinStr, clientId))
                {
                    Rpc40SetSkinStr.Serialize(w4, "skin_None", target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetSkinStr));
                    await game.FinishRpcAsync(w4, clientId);
                }
                using (var w5 = game.StartRpc(target.NetId, RpcCalls.SetVisorStr, clientId))
                {
                    Rpc42SetVisorStr.Serialize(w5, "visor_None", target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetVisorStr));
                    await game.FinishRpcAsync(w5, clientId);
                }
            }
        }

        private async Task MakePlayerVisibleAsync(IInnerPlayerControl target, IEnumerable<IClientPlayer> receivers = null)
        {
            var game = (receivers?.FirstOrDefault()?.Game) ?? target.Game;
            if (target?.PlayerInfo == null || game == null) return;

            var gameCosmetics = GetOrCreateGameCosmetics(game);
            if (!gameCosmetics.TryGetValue(target.OwnerId, out var snapshot)) return;

            // When receivers is null, broadcast to all
            if (receivers == null)
            {
                using (var w1 = game.StartRpc(target.NetId, RpcCalls.SetName))
                {
                    Rpc06SetName.Serialize(w1, target.NetId, snapshot.Name);
                    await game.FinishRpcAsync(w1);
                }
                using (var w2 = game.StartRpc(target.NetId, RpcCalls.SetColor))
                {
                    Rpc08SetColor.Serialize(w2, target.NetId, snapshot.Color);
                    await game.FinishRpcAsync(w2);
                }
                using (var w3 = game.StartRpc(target.NetId, RpcCalls.SetHatStr))
                {
                    Rpc39SetHatStr.Serialize(w3, snapshot.HatId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetHatStr));
                    await game.FinishRpcAsync(w3);
                }
                using (var w4 = game.StartRpc(target.NetId, RpcCalls.SetSkinStr))
                {
                    Rpc40SetSkinStr.Serialize(w4, snapshot.SkinId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetSkinStr));
                    await game.FinishRpcAsync(w4);
                }
                using (var w5 = game.StartRpc(target.NetId, RpcCalls.SetVisorStr))
                {
                    Rpc42SetVisorStr.Serialize(w5, snapshot.VisorId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetVisorStr));
                    await game.FinishRpcAsync(w5);
                }
                return;
            }

            foreach (var receiver in receivers)
            {
                if (receiver?.Client == null) continue;
                var clientId = receiver.Client.Id;

                using (var w1 = game.StartRpc(target.NetId, RpcCalls.SetName, clientId))
                {
                    Rpc06SetName.Serialize(w1, target.NetId, snapshot.Name);
                    await game.FinishRpcAsync(w1, clientId);
                }
                using (var w2 = game.StartRpc(target.NetId, RpcCalls.SetColor, clientId))
                {
                    Rpc08SetColor.Serialize(w2, target.NetId, snapshot.Color);
                    await game.FinishRpcAsync(w2, clientId);
                }
                using (var w3 = game.StartRpc(target.NetId, RpcCalls.SetHatStr, clientId))
                {
                    Rpc39SetHatStr.Serialize(w3, snapshot.HatId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetHatStr));
                    await game.FinishRpcAsync(w3, clientId);
                }
                using (var w4 = game.StartRpc(target.NetId, RpcCalls.SetSkinStr, clientId))
                {
                    Rpc40SetSkinStr.Serialize(w4, snapshot.SkinId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetSkinStr));
                    await game.FinishRpcAsync(w4, clientId);
                }
                using (var w5 = game.StartRpc(target.NetId, RpcCalls.SetVisorStr, clientId))
                {
                    Rpc42SetVisorStr.Serialize(w5, snapshot.VisorId, target.PlayerInfo.GetNextRpcSequenceId(RpcCalls.SetVisorStr));
                    await game.FinishRpcAsync(w5, clientId);
                }
            }
        }

        // Events: keep extremely simple per new rules
        [EventListener]
        public async ValueTask OnGameStart(IGameStartedEvent e)
        {
            _logger.LogDebug("Game started (simplified logic). Taking snapshots.");
            await TakeSnapshotsAtGameStart(e.Game);
        }

        [EventListener]
        public async ValueTask OnPlayerMurder(IPlayerMurderEvent e)
        {
            // After death: dead player sees EVERYONE unknown, unless impostor flag allows
            var game = e.Game;
            var deadClient = game.Players.FirstOrDefault(p => p.Client.Id == e.Victim.OwnerId);
            if (deadClient?.Character == null) return;

            // Game end check first
            if (await HandleGameEnd(game))
            {
                _logger.LogDebug("Game ended after murder; revealing everyone to everyone.");
                return;
            }

            if (allowImpostorsToSeeEveryone && deadClient.Character.PlayerInfo?.IsImpostor == true)
            {
                _logger.LogDebug("Victim is impostor and impostor-vision enabled; no unknowns sent.");
                return;
            }

            var everyoneElse = game.Players.Where(p => p.Client.Id != deadClient.Client.Id && p.Character != null).ToList();
            await MakePlayerUnknownAsyncTargetsForOneViewer(deadClient, everyoneElse);
        }

        [EventListener]
        public async ValueTask OnMeetingEnd(IMeetingEndedEvent e)
        {
            // Ejection happens here; apply rule: if a player is dead after meeting, they see everyone unknown
            var game = e.Game;

            // Game end check first
            if (await HandleGameEnd(game))
            {
                _logger.LogDebug("Game ended after meeting; revealing everyone.");
                return;
            }

            var deadPlayers = game.Players.Where(p => p.Character?.PlayerInfo?.IsDead == true).ToList();
            var alivePlayers = game.Players.Where(p => p.Character?.PlayerInfo?.IsDead == false).ToList();

            foreach (var dead in deadPlayers)
            {
                if (dead?.Character == null) continue;

                if (allowImpostorsToSeeEveryone && dead.Character.PlayerInfo?.IsImpostor == true)
                {
                    // impostor dead still sees everyone: ensure visible
                    foreach (var alive in alivePlayers)
                    {
                        if (alive?.Character != null)
                            await MakePlayerVisibleAsync(alive.Character, new[] { dead });
                    }
                    continue;
                }

                // normal dead: everyone unknown
                foreach (var alive in alivePlayers)
                {
                    if (alive?.Character != null)
                        await MakePlayerUnknownAsync(alive.Character, new[] { dead });
                }
            }
        }

        // Helper to invert perspective: make a set of targets unknown to a single viewer
        private async Task MakePlayerUnknownAsyncTargetsForOneViewer(IClientPlayer viewer, List<IClientPlayer> targets)
        {
            if (viewer?.Character == null) return;
            foreach (var t in targets)
            {
                if (t?.Character != null)
                    await MakePlayerUnknownAsync(t.Character, new[] { viewer });
            }
        }

        // Game end: everyone visible to everyone
        private async ValueTask<bool> HandleGameEnd(IGame game)
        {
            var players = game.Players.ToList();
            var alivePlayers = players.Where(p => p.Character?.PlayerInfo != null && !p.Character.PlayerInfo.IsDead).ToList();
            var impostors = alivePlayers.Count(p => p.Character?.PlayerInfo?.IsImpostor == true);
            var crew = alivePlayers.Count - impostors;
            if (impostors == crew || impostors == 0)
            {
                foreach (var p in players)
                {
                    if (p?.Character != null)
                        await MakePlayerVisibleAsync(p.Character); // broadcast
                }
                return true;
            }
            return false;
        }

        // Minimal snapshot + utilities retained
        private Dictionary<int, PlayerCosmetics> GetOrCreateGameCosmetics(IGame game)
        {
            if (!originalCosmeticsByGame.TryGetValue(game, out var map))
            {
                map = new Dictionary<int, PlayerCosmetics>();
                originalCosmeticsByGame[game] = map;
            }
            return map;
        }

        private PlayerCosmetics SnapshotPlayerCosmetics(IInnerPlayerControl player)
        {
            if (player?.PlayerInfo?.CurrentOutfit == null)
            {
                return new PlayerCosmetics
                {
                    Name = player?.PlayerInfo?.PlayerName ?? string.Empty,
                    Color = ColorType.Red,
                };
            }
            return new PlayerCosmetics
            {
                Name = player.PlayerInfo?.PlayerName ?? string.Empty,
                Color = player.PlayerInfo?.CurrentOutfit?.Color ?? ColorType.Red,
                HatId = player.PlayerInfo?.CurrentOutfit?.HatId ?? "hat_NoHat",
                SkinId = player.PlayerInfo?.CurrentOutfit?.SkinId ?? "skin_None",
                VisorId = player.PlayerInfo?.CurrentOutfit?.VisorId ?? "visor_None",
                NamePlateId = player.PlayerInfo?.CurrentOutfit?.NamePlateId ?? string.Empty,
            };
        }

        private Task TakeSnapshotsAtGameStart(IGame game)
        {
            var map = GetOrCreateGameCosmetics(game);
            foreach (var p in game.Players)
            {
                if (p?.Character == null) continue;
                map[p.Character.OwnerId] = SnapshotPlayerCosmetics(p.Character);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            originalCosmeticsByGame?.Clear();
        }
    }
}