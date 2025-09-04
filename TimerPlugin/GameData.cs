using Impostor.Api.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameTimerPlugin
{
    public class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> DeadPlayers { get; set; }
        public System.Threading.Timer? GameTimer { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsInMeeting { get; set; }
        public bool TimerIsUp { get; set; }
        public TimeSpan TimerDuration { get; set; }

        public delegate void TimerElapsedDelegate(GameData gameData);
        public event TimerElapsedDelegate? TimerElapsed;


        public void AddPlayer(IClientPlayer player, bool isImpostor)
        {
            Players.Add(player);
            if (isImpostor)
            {
                Impostors.Add(player);
            }
            else
            {
                Crewmates.Add(player);
            }
        }

        public void RemovePlayer(IClientPlayer player)
        {
            Players.Remove(player);
            Crewmates.Remove(player);
            Impostors.Remove(player);
            DeadPlayers.Remove(player);
        }

        public void MarkAsDead(IClientPlayer player)
        {
            DeadPlayers.Add(player);
            Crewmates.Remove(player);
            Impostors.Remove(player);
        }

        public void MeetingStarted()
        {
            IsInMeeting = true;
        }

        public void MeetingEnded()
        {
            IsInMeeting = false;
        }

        public bool IsTimerUp()
        {
            return TimerIsUp;
        }

        public void StartTimer(TimeSpan duration)
        {
            StartTime = DateTime.UtcNow;
            GameTimer = new System.Threading.Timer(TimerCallback, null, duration, Timeout.InfiniteTimeSpan);
            TimerDuration = duration;
        }

        private void TimerCallback(object? state)
        {
            TimerIsUp = true;
            TimerElapsed?.Invoke(this);  // Pass 'this' as the argument to the delegate
        }


        public void StopTimer()
        {
            GameTimer?.Dispose();
        }

        public void ResetGame()
        {
            StopTimer();
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            DeadPlayers = new List<IClientPlayer>();
            IsInMeeting = false;
            TimerIsUp = false;
        }

        public GameData()
        {
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            DeadPlayers = new List<IClientPlayer>();
            IsInMeeting = false;
            TimerIsUp = false;
            StartTime = DateTime.UtcNow;
        }

    }
}
