using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MatchLog
{
    public class GameData {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Crewmates { get; set; }
        public List<IClientPlayer> Impostors { get; set; }
        public List<IClientPlayer> deadPlayers { get; set; }
        public List<String> eventLogging { get; set; }
        public string gameCode { get; set; }
        public string gameStarted { get; set; }
        public DateTime gameStartedUTC { get; set; }
        public int countdown { get; set; }
        public bool canceled { get; set; }
        public bool inMeeting = false;
        public int matchId { get; set; }


        public GameData(string code, int id)
        {
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            deadPlayers = new List<IClientPlayer>();
            eventLogging = new List<String>();
            gameCode = code;
            countdown = 300 + 6; //Six seconds of delay seems to hit the sweet spot
            canceled = false;
            matchId = id;
        }

        public void AddPlayer(IClientPlayer player, bool isImpostor)
        {
            Players.Add(player);
            if (!isImpostor)
            {
                Crewmates.Add(player);
            }
            else
            {
                Impostors.Add(player);
            }
        }


        public void gameStartTimeSet()
        {
            var timeStart = DateTime.Now;
            gameStartedUTC = timeStart;
            gameStarted = $"0 | {timeStart} | {gameCode}";
            eventLogging.Add(gameStarted);

        }

        public void addTaskCompletion(IClientPlayer player, DateTime timeOfCompletion, TaskCategories taskType, TaskTypes taskName)
        {
            eventLogging.Add($"1 | {timeOfCompletion} | {player.Character.PlayerInfo.PlayerName.TrimEnd()} | {taskType} | {taskName}");
        }

        public void onDeathEvent(IClientPlayer player, DateTime timeOfDeath, IClientPlayer killer)
        {
            eventLogging.Add($"2 | {timeOfDeath} | {player.Character.PlayerInfo.PlayerName.TrimEnd()} | {killer.Character.PlayerInfo.PlayerName.TrimEnd()}");
        }

/*        public void finalHideStarted(DateTime finalHideStart)
        {
            eventLogging.Add($"3 | {finalHideStart}");
        }*/

        public void gameEnd(DateTime endOfGame, string endReason)
        {
            eventLogging.Add($"4 | {endOfGame} | {endReason} | {gameCode}");
        }

        public void startMeeting(string playername, DateTime timeOfMeetingStart)
        {
            var _impostors = string.Join(",", Impostors.Select(p=> p.Character.PlayerInfo.PlayerName.TrimEnd()));
            eventLogging.Add($"5 | {timeOfMeetingStart} | {gameCode} | {playername.TrimEnd()} | {_impostors}");
        }

        public void endMeeting(DateTime timeOfMeetingEnd, string result)
        {
            var _impostors = string.Join(",", Impostors.Select(p => p.Character.PlayerInfo.PlayerName.TrimEnd()));
            eventLogging.Add($"6 | {timeOfMeetingEnd} | {gameCode} | {result} | {_impostors}");
        }

        public void addVote(string playername, DateTime timeOfVote, string voted, VoteType voteType) {
            eventLogging.Add($"7 | {timeOfVote} | {playername.TrimEnd()} | {voted.TrimEnd()} | {voteType}");
        }

        public void onCancel(string playername, DateTime timeOfCancel)
        {
            eventLogging.Add($"8 | {timeOfCancel} | {playername.TrimEnd()}");
        }

        public void onReport(string playername, string dead_playername, DateTime timeOfBodyReport)
        {
            var _impostors = string.Join(",", Impostors.Select(p => p.Character.PlayerInfo.PlayerName.TrimEnd()));
            eventLogging.Add($"9 | {timeOfBodyReport} | {playername.TrimEnd()} | {dead_playername.TrimEnd()} | {_impostors}");
        }

        public void onEnd(IClientPlayer player, DateTime endTime)
        {
            eventLogging.Add($"10 | {endTime} | {player.Character.PlayerInfo.PlayerName.TrimEnd()}");
        }

        public void onExile(string exiled, DateTime exileTime)
        {
            eventLogging.Add($"11 | {exileTime} | {exiled.TrimEnd()}");
        }

        public void disconnectedPlayer(DateTime dcTime, IClientPlayer player)
        {
            if (Impostors.Contains(player) && Impostors.Count == 2) {
                if (Impostors[0].Equals(player))
                {
                    Impostors[0] = Impostors[1];
                    Impostors[1] = player;
                }
            }
            eventLogging.Add($"99 | {dcTime} | {player.Character.PlayerInfo.PlayerName.TrimEnd()}");
        }



        public void ResetGame()
        {
            Players = new List<IClientPlayer>();
            Crewmates = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
            deadPlayers = new List<IClientPlayer>();
            eventLogging = new List<String>();
        }

        public List<string> stringifyData()
        {
            string playerNames = string.Join(",", Players.Select(p =>
            {
                string playerName = p.Character.PlayerInfo.PlayerName;
                if (playerName == "\u003C#FD0\u003EVex" || playerName == "<#FD0>Vex")
                {
                    return "Vex";
                }
                return playerName;
            }));

            //string impostor = Impostor.Character.PlayerInfo.PlayerName;
            string impostors = string.Join(", ", Impostors.Select(p =>
            {
                string impostor = p.Character.PlayerInfo.PlayerName;
                if (impostor == "\u003C#FD0\u003EVex" || impostor == "<#FD0>Vex")
                {
                    return "Vex";
                }
                return impostor;
            }));

            return new List<string> { gameStartedUTC.ToString(), playerNames, impostors};
        }

        public string jsonifyRawData()
        {
            // Assume this is your list of strings
            // Create a list to hold the parsed data
            List<Dictionary<string, object>> data = new();

            // Parse each string and add the data to the list
            foreach (string str in eventLogging)
            {
                var parts = str.Split(" | ");
                /*var item = new Dictionary<string, object>
                {
                    { "time", parts[0].Trim() },
                    { "name", parts[1].Trim() },
                    { "currentCount", int.Parse(parts[2].Trim()) }
                };*/
                var item = new Dictionary<string, object>();
                switch (parts[0].Trim())
                {
                    case "0":
                        item.Add("Event", "StartGame");
                        item.Add("Time", parts[1].Trim());
                        item.Add("GameCode", parts[2].Trim());
                        break;
                    case "1":
                        item.Add("Event", "Task");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Name", parts[2].Trim());
                        item.Add("TaskType", parts[3].Trim());
                        item.Add("TaskName", parts[4].Trim());
                        break;
                    case "2":
                        item.Add("Event", "Death");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Name", parts[2].Trim());
                        item.Add("Killer", parts[3].Trim());
                        break;
                    case "3":
                        item.Add("Event", "FinalHide");
                        item.Add("Time", parts[1].Trim());
                        break;
                    case "4":
                        item.Add("Event", "EndGame");
                        item.Add("Time", parts[1].Trim());
                        item.Add("WinReason", parts[2].Trim());
                        item.Add("GameCode", parts[3].Trim());
                        break;
                    case "5":
                        item.Add("Event", "MeetingStart");
                        item.Add("Time", parts[1].Trim());
                        item.Add("GameCode", parts[2].Trim());
                        item.Add("Player", parts[3].Trim());
                        item.Add("Impostors", parts[4].Trim());
                        break;
                    case "6":
                        item.Add("Event", "MeetingEnd");
                        item.Add("Time", parts[1].Trim());
                        item.Add("GameCode", parts[2].Trim());
                        item.Add("Result", parts[3].Trim());
                        item.Add("Impostors", parts[4].Trim());
                        break;
                    case "7":
                        item.Add("Event", "PlayerVote");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Player", parts[2].Trim());
                        item.Add("Target", parts[3].Trim());
                        item.Add("Type", parts[4].Trim());
                        break;
                    case "8":
                        item.Add("Event", "GameCancel");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Player", parts[2].Trim());
                        break;
                    case "9":
                        item.Add("Event", "BodyReport");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Player", parts[2].Trim());
                        item.Add("DeadPlayer", parts[3].Trim());
                        item.Add("Impostors", parts[4].Trim());
                        break;
                    case "10":
                        item.Add("Event", "ManualGameEnd");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Player", parts[2].Trim());
                        break;
                    case "11":
                        item.Add("Event", "Exiled");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Player", parts[2].Trim());
                        break;
                    case "99":
                        item.Add("Event", "Disconnect");
                        item.Add("Time", parts[1].Trim());
                        item.Add("Name", parts[2].Trim());
                        break;
                    default:
                        item.Add("Event", "ERROR");
                        break;
                }

                data.Add(item);
            }

            // Serialize the data to JSON
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return json;
        }


        public void DecreaseCountdown(int delta)
        {
            countdown -= delta;
        }

        public void DecreaseCountdownByOne()
        {
            countdown -= 1;
        }
    }
}
