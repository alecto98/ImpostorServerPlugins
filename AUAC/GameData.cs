using Impostor.Api.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AuAc
{
    internal class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> DeadPlayers { get; set; }
        public string GameCode { get; set; }

        public bool IsMeetingTimerRunning { get; private set; } = false; // Track if the timer is running
        public int VotingTime { get; private set; } // Store the voting time in seconds
        public DateTime MeetingTimerStartTime { get; private set; } // Store the start time of the timer


        public GameData(string code, int votingTime)
        {
            GameCode = code;
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            DeadPlayers = new List<IClientPlayer>();
            VotingTime = votingTime;
        }

        public void AddPlayer(IClientPlayer player)
        {
            if (player == null)
            {
                return;
            }

            var info = player.Character?.PlayerInfo;
            if (info?.IsImpostor == true)
            {
                Impostors.Add(player);
            }
            else if (info != null)
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
