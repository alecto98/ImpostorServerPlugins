using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Replay
{
    public class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> DeadPlayers { get; set; }
        public List<string> EventLogging { get; set; }
        public string GameCode { get; set; }
        public string GameStarted { get; set; }
        public DateTime GameStartedUTC { get; set; }
        public bool InMeeting { get; set; }
        public bool Canceled { get; set; }

        public GameData(string code)
        {
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            DeadPlayers = new List<IClientPlayer>();
            EventLogging = new List<string>();
            Canceled = false;
            GameCode = code;
        }

        public void AddPlayer(IClientPlayer player, bool isImpostor)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding player: {ex.Message}");
            }
        }

        public void GameStartTimeSet()
        {
            var timeStart = DateTime.Now;
            GameStartedUTC = timeStart;
            GameStarted = $"0 | {timeStart}";
            EventLogging.Add(GameStarted);
        }
        public void OnMovement(string playername, string location, TimeSpan timeOfMovement)
        {
            EventLogging.Add($"1 | {playername} | {location} | {timeOfMovement}");
        }
        public void OnTaskFinish(string playerName, string taskName, string playerPosition, TimeSpan timeOfDeath)
        {
            EventLogging.Add($"2 | {playerName} | {taskName} | {playerPosition} | {timeOfDeath}");
        }
        public void OnDeathEvent(string player, string killer, string playerPosition, TimeSpan timeOfDeath)
        {
            EventLogging.Add($"3 | {player} | {killer} | {playerPosition} | {timeOfDeath}");
        }
        public void OnEnterVent(string playername, string ventname, string playerPosition, TimeSpan endTime)
        {
            EventLogging.Add($"4 | {playername} | {ventname} | {playerPosition} | {endTime}");
        }
        public void OnExitVent(string playername, string ventname, string playerPosition, TimeSpan endTime)
        {
            EventLogging.Add($"5 | {playername} | {ventname} | {playerPosition} | {endTime}");
        }
        public void OnReport(string playername, string dead_playername, string playerPosition, TimeSpan timeOfBodyReport)
        {
            EventLogging.Add($"6 | {playername} | {dead_playername} | {playerPosition} | {timeOfBodyReport}");
        }
        public void StartMeeting(string playername, TimeSpan timeOfMeetingStart)
        {
            EventLogging.Add($"7 | {playername} | {timeOfMeetingStart}");
        }
        public void OnVote(string playerName, string voted, TimeSpan timeOfVote)
        {
            EventLogging.Add($"8 | {playerName} | {voted} | {timeOfVote}");
        }
        public void EndMeeting(string result, TimeSpan timeOfMeetingEnd)
        {
            EventLogging.Add($"9 | {result} | {timeOfMeetingEnd}");
        }
        public void GameEnd(string endReason, TimeSpan timeofGameEnd)
        {
            EventLogging.Add($"10 | {endReason} | {timeofGameEnd}");
        }


        public void ResetGame()
        {
            Players.Clear();
            Crewmates.Clear();
            Impostors.Clear();
            DeadPlayers.Clear();
            EventLogging.Clear();
        }

        public List<string> StringifyData()
        {
            string playerNames = string.Join(",", Players.Select(p => p.Character.PlayerInfo.PlayerName));
            string colors = string.Join(",", Players.Select(p => p.Character.PlayerInfo.CurrentOutfit.Color.ToString()));
            string impostors = string.Join(",", Impostors.Select(p => p.Character.PlayerInfo.PlayerName));

            return new List<string> { GameStartedUTC.ToString(), playerNames, colors, impostors };
        }

        public string JsonifyRawData()
        {
            List<Dictionary<string, object>> data = new();

            foreach (string str in EventLogging)
            {
                var parts = str.Split(" | ");
                var item = new Dictionary<string, object>();

                switch (parts[0].Trim())
                {
                    case "0":
                        item.Add("Event", "StartGame");
                        item.Add("Time", parts[1].Trim());
                        break;
                    case "1":
                        item.Add("Event", "Movement");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Location", parts[2].Trim());
                        item.Add("Time", parts[3].Trim());
                        break;
                    case "2":
                        item.Add("Event", "TaskFinish");
                        item.Add("Player", parts[1].Trim());
                        item.Add("TaskName", parts[2].Trim());
                        item.Add("Time", parts[3].Trim());
                        break;
                    case "3":
                        item.Add("Event", "Death");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Killer", parts[2].Trim());
                        item.Add("Location", parts[3].Trim());
                        item.Add("Time", parts[4].Trim());
                        break;
                    case "4":
                        item.Add("Event", "EnterVent");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Vent", parts[2].Trim());
                        item.Add("Location", parts[3].Trim());
                        item.Add("Time", parts[4].Trim());
                        break;
                    case "5":
                        item.Add("Event", "ExitVent");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Vent", parts[2].Trim());
                        item.Add("Location", parts[3].Trim());
                        item.Add("Time", parts[4].Trim());
                        break;
                    case "6":
                        item.Add("Event", "BodyReport");
                        item.Add("Player", parts[1].Trim());
                        item.Add("DeadPlayer", parts[2].Trim());
                        item.Add("Location", parts[3].Trim());
                        item.Add("Time", parts[4].Trim());
                        break;
                    case "7":
                        item.Add("Event", "MeetingStart");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Time", parts[2].Trim());
                        break;
                    case "8":
                        item.Add("Event", "Vote");
                        item.Add("Player", parts[1].Trim());
                        item.Add("Voted", parts[2].Trim());
                        item.Add("Time", parts[3].Trim());
                        break;
                    case "9":
                        item.Add("Event", "MeetingEnd");
                        item.Add("Result", parts[1].Trim());
                        item.Add("Time", parts[2].Trim());
                        break;
                    case "10":
                        item.Add("Event", "EndGame");
                        item.Add("WinReason", parts[1].Trim());
                        item.Add("Time", parts[2].Trim());
                        break;
                    default:
                        item.Add("Event", "ERROR");
                        break;
                }

                data.Add(item);
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return json;
        }
    }
}
