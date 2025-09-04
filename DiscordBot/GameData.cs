using Impostor.Api.Net;
using System;
using System.Collections.Generic;
using System.Timers;
using Timer = System.Timers.Timer; // Import Timer namespace

namespace DiscordBot
{
    internal class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> DeadPlayers { get; set; }
        public string GameCode { get; set; }

        public Timer MeetingTimer { get; private set; } // Timer for meeting
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
            MeetingTimer = new Timer(votingTime * 1000); // Convert voting time to milliseconds
            MeetingTimer.Elapsed += OnTimerElapsed; // Subscribe to the timer event
            MeetingTimer.AutoReset = false; // Make sure the timer doesn't automatically reset
        }

        public void AddPlayer(IClientPlayer player)
        {
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
            ResetMeetingTimer();
        }

        public void StartMeetingTimer()
        {
            if (!IsMeetingTimerRunning)
            {
                MeetingTimerStartTime = DateTime.Now; // Track the start time
                MeetingTimer.Start();
                IsMeetingTimerRunning = true;
            }
        }
        public TimeSpan GetMeetingTimerElapsed()
        {
            if (IsMeetingTimerRunning)
            {
                return DateTime.Now - MeetingTimerStartTime; // Calculate elapsed time
            }
            return TimeSpan.Zero;
        }

        public void ResetMeetingTimer()
        {
            if (IsMeetingTimerRunning)
            {
                MeetingTimer.Stop();
                IsMeetingTimerRunning = false;
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // This will trigger when the timer finishes
            IsMeetingTimerRunning = false;
        }
    }
}
