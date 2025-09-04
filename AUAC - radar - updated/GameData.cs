using Impostor.Api.Net;
using Impostor.Api.Innersloth.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AuAc
{
    public class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> DeadPlayers { get; set; }
        public string GameCode { get; set; }
        
        // Game settings for anti-cheat
        public float CrewmateVision { get; set; }
        public float ImpostorVision { get; set; }
        public int Map { get; set; } // Using int instead of MapTypes
        public const float VISION_MULTIPLIER = 6.0f; // Static multiplier for testing

        public bool IsMeetingTimerRunning { get; private set; } = false;
        public int VotingTime { get; private set; }
        public DateTime MeetingTimerStartTime { get; private set; }

        public GameData(string code, int votingTime)
        {
            GameCode = code;
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            DeadPlayers = new List<IClientPlayer>();
            VotingTime = votingTime;
        }

        public void UpdateGameSettings(NormalGameOptions? options)
        {
            if (options == null) return;
            
            CrewmateVision = options.CrewLightMod * VISION_MULTIPLIER;
            ImpostorVision = options.ImpostorLightMod * VISION_MULTIPLIER;
            Map = (int)options.Map;
        }

        public void AddPlayer(IClientPlayer player)
        {
            if (player?.Character?.PlayerInfo == null) return;
            
            if (player.Character.PlayerInfo.IsImpostor)
            {
                Impostors.Add(player);
            }
            else
            {
                Crewmates.Add(player);
            }
            Players.Add(player);
        }

        public void ResetGame()
        {
            Players.Clear();
            Crewmates.Clear();
            Impostors.Clear();
            DeadPlayers.Clear();
        }
    }
}
