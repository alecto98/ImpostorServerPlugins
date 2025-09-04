using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Innersloth.GameOptions.RoleOptions;
using Impostor.Api.Net;
using Microsoft.Extensions.Logging;

namespace LockedGameSettingsPlugin;

public sealed class LockedGameSettingsListener : IEventListener
{
    private readonly ILogger<LockedGameSettingsListener> _logger;
    private readonly NormalGameOptions _options;
    private readonly RoleValues _rolevalues;
    private Dictionary<GameCode, GameData> gameDataMap { get; set; }
    private MapTypes mapType = MapTypes.Polus;
    private bool changeMap = false;

    public LockedGameSettingsListener(ILogger<LockedGameSettingsListener> logger, NormalGameOptions options, RoleValues rolevalues)
    {
        _logger = logger;
        _options = options;
        _rolevalues = rolevalues;
        gameDataMap = new Dictionary<GameCode, GameData>();
    }

    public static void ApplyGameOptions(NormalGameOptions from, NormalGameOptions into, RoleValues rolevalues)
    {
        RoleOptionsCollection.RoleData into_data = default;
        RoleValues values = new RoleValues(rolevalues);
        into.MaxPlayers = from.MaxPlayers;
        into.Keywords = from.Keywords;
        into.Map = from.Map;
        into.NumImpostors = from.NumImpostors;
        into.IsDefaults = from.IsDefaults;
        into.PlayerSpeedMod = from.PlayerSpeedMod;
        into.CrewLightMod = from.CrewLightMod;
        into.ImpostorLightMod = from.ImpostorLightMod;
        into.KillCooldown = from.KillCooldown;
        into.NumCommonTasks = from.NumCommonTasks;
        into.NumLongTasks = from.NumLongTasks;
        into.NumShortTasks = from.NumShortTasks;
        into.NumEmergencyMeetings = from.NumEmergencyMeetings;
        into.EmergencyCooldown = from.EmergencyCooldown;
        into.GhostsDoTasks = from.GhostsDoTasks;
        into.KillDistance = from.KillDistance;
        into.DiscussionTime = from.DiscussionTime;
        into.VotingTime = from.VotingTime;
        into.ConfirmImpostor = from.ConfirmImpostor;
        into.VisualTasks = from.VisualTasks;
        into.AnonymousVotes = from.AnonymousVotes;
        into.TaskBarUpdate = from.TaskBarUpdate;
        
        //Roles
        var version = (byte)NormalGameOptions.LatestVersion;
        EngineerRoleOptions engineer = new EngineerRoleOptions(version);
        engineer.Cooldown = (byte)values.engineerCoolDown;
        engineer.InVentMaxTime = (byte)values.engineerInVentMaxTime;
        var engineerRate = createRate((byte)values.engineerCount, (byte)values.engineerChance);
        ScientistRoleOptions scientist = new ScientistRoleOptions(version);
        scientist.Cooldown = (byte)values.scientistCooldown;
        scientist.BatteryCharge = (byte)values.scientistBatteryCharge;
        var scientistRate = createRate((byte)values.scientistCount, (byte)values.scientistChance);
        GuardianAngelRoleOptions GA = new GuardianAngelRoleOptions(version);
        GA.Cooldown = (byte)values.GACooldown;
        var GARate = createRate((byte)values.GACount, (byte)values.GAChance);
        ShapeshifterRoleOptions shapeshifter = new ShapeshifterRoleOptions(version);
        shapeshifter.Cooldown = (byte)values.shapeshifterCooldown;
        shapeshifter.Duration = (byte)values.shapeshifterDuration;
        shapeshifter.LeaveSkin = values.shapeshifterLeaveSkin;
        var shapeshifterRate = createRate((byte)values.shapeshifterCount, (byte)values.shapeshifterChance);
        from.RoleOptions.Roles.Clear();
        var engineerRoleData = createRoleData(RoleTypes.Engineer, engineer, engineerRate);
        var scientistRoleData = createRoleData(RoleTypes.Scientist, scientist, scientistRate);
        var GARoleData = createRoleData(RoleTypes.GuardianAngel, GA, GARate);
        var shapeshifterRoleData = createRoleData(RoleTypes.Shapeshifter, shapeshifter, shapeshifterRate);
        from.RoleOptions.Roles.Add(RoleTypes.Engineer, engineerRoleData);
        from.RoleOptions.Roles.Add(RoleTypes.Scientist, scientistRoleData);
        from.RoleOptions.Roles.Add(RoleTypes.GuardianAngel, GARoleData);
        from.RoleOptions.Roles.Add(RoleTypes.Shapeshifter, shapeshifterRoleData);
        into.RoleOptions = from.RoleOptions;

    }

    public static RoleOptionsCollection.RoleData createRoleData(RoleTypes type, IRoleOptions roleOptions, RoleRate rate)
    {
        return new RoleOptionsCollection.RoleData(type, roleOptions, rate);
    }

    public static RoleRate createRate(byte maxCount, byte Chance)
    {
        return new RoleRate(maxCount, Chance);
    }

    private async ValueTask SetSettingsAsync(IGame game, IClientPlayer host)
    {
        if (game.Options is NormalGameOptions gameOptions)
        {
            _logger.LogInformation($"Applying settings for game: {game.Code}");
            ApplyGameOptions(gameDataMap[game.Code].gameOptions, gameOptions, _rolevalues);
            _logger.LogInformation($"SyncingSettings for game: {game.Code}");
            if (game.Code == host.Game.Code) {
                await game.SyncSettingsAsync();
            }
            
        }
    }

    [EventListener]
    public void onGameCreate(IGameCreatedEvent e) {
        if (!gameDataMap.ContainsKey(e.Game.Code)) {
            GameData data = new GameData(_options);
            gameDataMap.Add(e.Game.Code, data);
        }
    }

    [EventListener]
    public async ValueTask OnPlayerSpawnedEvent(IPlayerSpawnedEvent e)
    {
        _logger.LogInformation($"PlayerSpawnedEvent called in game: {e.Game.Code}");
        IClientPlayer player = e.ClientPlayer;
        if (player.IsHost) {
            await SetSettingsAsync(e.Game, player);
        }
    }
    
    [EventListener]
    public async ValueTask OnGameStartingEvent(IGameStartingEvent e)
    {
        _logger.LogInformation($"GameStartingEvent called in game: {e.Game.Code}");
        if (e.Game.Host == null) return; 
        await SetSettingsAsync(e.Game, e.Game.Host);
        if (e.Game.PlayerCount != 10)
        {
            foreach (var player in e.Game.Players)
            {
                //await player.Client.DisconnectAsync(DisconnectReason.Custom, "A Ranked game needs 10 Players.");
            }
        }
    }

    [EventListener]
    public void onChangeMap(IPlayerChatEvent e) 
    {
        string prefix = "/map";
        string message = "";
        if (e.ClientPlayer.Character == null) return;
        if (e.ClientPlayer.IsHost && (e.Message.StartsWith(prefix) && e.Game.GameState == GameStates.NotStarted))
        {
            switch (e.Message.Split(" ")[1].ToUpper().Trim())
            {
                case "SKELD":
                    mapType = MapTypes.Skeld;
                    message = "Map Changed to Skeld";
                    break;
                case "MIRAHQ":
                    mapType = MapTypes.MiraHQ;
                    message = "Map Changed to MiraHQ";
                    break;
                case "POLUS":
                    mapType = MapTypes.Polus;
                    message = "Map Changed to Polus";
                    break;
                case "AIRSHIP":
                    mapType = MapTypes.Airship;
                    message = "Map Changed to Airship";
                    break;
                default:
                    message = "Unnown Map choose between the following: skeld | mirahq | polus | airship ";
                    break;
            }
            if (message != "") e.ClientPlayer.Character.SendChatToPlayerAsync(message);
        }
        else { return; }
        if (gameDataMap.ContainsKey(e.Game.Code))
        {
            var temp = gameDataMap[e.Game.Code];
            _options.Map = mapType;
            _logger.LogInformation($"Before change - Game: {e.Game.Code} Map: {gameDataMap[e.Game.Code].gameOptions.Map}");
            temp.setOptions(_options);
            _logger.LogInformation($"After change - Game: {e.Game.Code} Map: {gameDataMap[e.Game.Code].gameOptions.Map}");
        }
    }
    
}
