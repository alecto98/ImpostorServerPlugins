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
using RabbitMQ.Client;
using System.Net.Sockets;
using System.Numerics;
using Impostor.Api.Innersloth.Maps;

namespace DiscordBot
{
    public class allowedVersionsListener : IEventListener
    {
        
        private readonly ILogger<AllowedVersions> _logger;
        private readonly string hostName = "localhost";
        private IEventManager _eventManager;

        public allowedVersionsListener(ILogger<AllowedVersions> logger, IEventManager eventManager)
        {   
            _logger = logger;
            _eventManager = eventManager;
        }

       
        [EventListener]
        public void DenyWrongVersion(IGamePlayerJoinedEvent e)
        {
            string workingDirectory = Environment.CurrentDirectory;
            string filePath = Path.Combine(workingDirectory, "plugins", "allowedVersion.txt");

            if (!File.Exists(filePath))
            {
                try
                {
                    File.Create(filePath).Dispose();
                    _logger.LogInformation($"Allowed version file not found. Created an empty file at {filePath}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred while creating the allowed versions file: {ex.Message}");
                    return;
                }
            }

            List<string> allowedVersions;
            try
            {
                allowedVersions = File.ReadAllLines(filePath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while reading the allowed versions list: {ex.Message}");
                return;
            }
            if (!allowedVersions.Contains(e.Player.Client.GameVersion.ToString()))

            {
                e.Player.Client.DisconnectAsync(DisconnectReason.IncorrectVersion);
                _logger.LogError($"Player is using version:{e.Player.Client.GameVersion.ToString()} - no match found.");

            }
        }

    }
}


//}
