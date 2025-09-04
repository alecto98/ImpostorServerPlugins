using Impostor.Api.Events.Player;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace MatchLog
{
	public class GameData
	{
		public List<IClientPlayer> Players { get; set; }
		public List<IClientPlayer> Crewmates { get; set; }
		public List<IClientPlayer> Impostors { get; set; }
		public List<IClientPlayer> DeadPlayers { get; set; }
		public List<string> EventLogging { get; set; }
		public string GameCode { get; set; }
		public string? GameStarted { get; set; }
		public DateTime GameStartedUTC { get; set; }
		public bool InMeeting { get; set; }
		public bool Canceled { get; set; }
		public int matchId { get; set; }

		private Dictionary<string, Vector2> playerPositions;
		private DateTime lastMovementLogTime;
		private const double MOVEMENT_LOG_INTERVAL = 0.2; // 0.2 seconds
		private int initialPlayerCount;

		public List<VoteData> Votes { get; set; }

		public GameData(string code, int id)
		{
			Players = new List<IClientPlayer>();
			Crewmates = new List<IClientPlayer>();
			Impostors = new List<IClientPlayer>();
			DeadPlayers = new List<IClientPlayer>();
			EventLogging = new List<string>();
			playerPositions = new Dictionary<string, Vector2>();
			Votes = new List<VoteData>();
			Canceled = false;
			GameCode = code;
			matchId = id;
			lastMovementLogTime = DateTime.MinValue;
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

				if (player.Character?.PlayerInfo != null)
				{
					string playerName = player.Character.PlayerInfo.PlayerName;
					playerPositions[playerName] = Vector2.Zero;
				}
			}
			catch (Exception)
			{
				// ignore
			}
		}

		public void GameStartTimeSet()
		{
			var timeStart = DateTime.Now;
			GameStartedUTC = timeStart;
			GameStarted = $"0 | {timeStart}";
			EventLogging.Add(GameStarted);
			lastMovementLogTime = timeStart;
			initialPlayerCount = Math.Min(Players.Count, 15);
		}

		public void UpdatePlayerPosition(string playerName, Vector2 position, bool isDead)
		{
			if (isDead)
			{
				playerPositions[playerName] = Vector2.Zero;
			}
			else
			{
				playerPositions[playerName] = position;
			}

			DateTime currentTime = DateTime.Now;
			if ((currentTime - lastMovementLogTime).TotalSeconds >= MOVEMENT_LOG_INTERVAL)
			{
				LogAllPlayerPositions(currentTime);
				lastMovementLogTime = currentTime;
			}
		}

		private void LogAllPlayerPositions(DateTime currentTime)
		{
			TimeSpan timeElapsed = currentTime - GameStartedUTC;
			string[] positions = new string[initialPlayerCount];
			int index = 0;
			foreach (var player in Players)
			{
				if (index >= initialPlayerCount) break;
				if (player?.Character?.PlayerInfo?.PlayerName != null)
				{
					string playerName = player.Character.PlayerInfo.PlayerName;
					if (playerPositions.ContainsKey(playerName))
					{
						Vector2 pos = playerPositions[playerName];
						positions[index] = $"{pos.X},{pos.Y}";
					}
					else
					{
						positions[index] = "0,0";
					}
				}
				else
				{
					positions[index] = "0,0";
				}
				index++;
			}
			for (int i = index; i < initialPlayerCount; i++)
			{
				positions[i] = "0,0";
			}
			string allPositions = string.Join("|", positions);
			EventLogging.Add($"1 | {allPositions} | {timeElapsed}");
		}

		public void OnTaskFinish(string playerName, string taskName, string playerPosition, TimeSpan timeOfTaskFinish)
		{
			EventLogging.Add($"2 | {playerName} | {taskName} | {playerPosition} | {timeOfTaskFinish}");
		}

		public void OnDeathEvent(string player, string killer, string playerPosition, TimeSpan timeOfDeath)
		{
			EventLogging.Add($"3 | {player} | {killer} | {playerPosition} | {timeOfDeath}");
			if (playerPositions.ContainsKey(player))
			{
				playerPositions[player] = Vector2.Zero;
			}
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

        public void addVote(string player, DateTime voteTime, string target, VoteType voteType)
		{
			TimeSpan timeElapsed = voteTime - GameStartedUTC;
			Votes.Add(new VoteData
			{
				Player = player,
				Target = target,
				VoteType = voteType,
				Time = timeElapsed
			});
			EventLogging.Add($"7 | {timeElapsed} | {player} | {target} | {voteType}");
		}

		public void StartMeeting(string playername, TimeSpan timeOfMeetingStart)
		{
			EventLogging.Add($"8 | {playername} | {timeOfMeetingStart}");
		}

		public void EndMeeting(string result, TimeSpan timeOfMeetingEnd)
		{
			EventLogging.Add($"9 | {result} | {timeOfMeetingEnd}");
		}

		public void OnExile(string exiledPlayerName, DateTime actualTime)
		{
			EventLogging.Add($"11 | {exiledPlayerName} | {actualTime}");
		}

		public void GameEnd(string endReason, TimeSpan timeofGameEnd)
		{
			EventLogging.Add($"10 | {endReason} | {timeofGameEnd}");
		}

		public void OnDisconnect(string playerName, DateTime actualTime)
		{
			EventLogging.Add($"99 | {actualTime} | {playerName}");
		}

		public void ResetGame()
		{
			Players.Clear();
			Crewmates.Clear();
			Impostors.Clear();
			DeadPlayers.Clear();
			EventLogging.Clear();
			playerPositions.Clear();
			Votes.Clear();
		}

		public List<string> StringifyData()
		{
			string playerNames = string.Join(",",
				Players
					.Where(p => p?.Character?.PlayerInfo != null)
					.Select(p => p.Character!.PlayerInfo!.PlayerName ?? "Unknown"));

			string colors = string.Join(",",
				Players
					.Where(p => p?.Character?.PlayerInfo != null)
					.Select(p => p.Character!.PlayerInfo!.CurrentOutfit.Color.ToString()));

			string impostors = string.Join(",",
				Impostors
					.Where(p => p?.Character?.PlayerInfo != null)
					.Select(p => p.Character!.PlayerInfo!.PlayerName ?? "Unknown"));

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
						item.Add("Event", "Pos");
						item.Add("Positions", parts[1].Trim());
						item.Add("Time", parts[2].Trim());
						break;
					case "2":
						item.Add("Event", "TaskFinish");
						item.Add("Player", parts[1].Trim());
						item.Add("TaskName", parts[2].Trim());
						item.Add("Position", parts[3].Trim());
						item.Add("Time", parts[4].Trim());
						break;
					case "3":
						item.Add("Event", "Death");
						item.Add("Player", parts[1].Trim());
						item.Add("Killer", parts[2].Trim());
						item.Add("Position", parts[3].Trim());
						item.Add("Time", parts[4].Trim());
						break;
					case "4":
						item.Add("Event", "EnterVent");
						item.Add("Player", parts[1].Trim());
						item.Add("Vent", parts[2].Trim());
						item.Add("Position", parts[3].Trim());
						item.Add("Time", parts[4].Trim());
						break;
					case "5":
						item.Add("Event", "ExitVent");
						item.Add("Player", parts[1].Trim());
						item.Add("Vent", parts[2].Trim());
						item.Add("Position", parts[3].Trim());
						item.Add("Time", parts[4].Trim());
						break;
					case "6":
						item.Add("Event", "BodyReport");
						item.Add("Player", parts[1].Trim());
						item.Add("DeadPlayer", parts[2].Trim());
						item.Add("Position", parts[3].Trim());
						item.Add("Time", parts[4].Trim());
						break;
					case "7":
						item.Add("Event", "PlayerVote");
						item.Add("Time", parts[1].Trim());
						item.Add("Player", parts[2].Trim());
						item.Add("Target", parts[3].Trim());
						item.Add("Type", parts[4].Trim());
						break;
					case "8":
						item.Add("Event", "MeetingStart");
						item.Add("Player", parts[1].Trim());
						item.Add("Time", parts[2].Trim());
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
					case "11":
						// Exiled: include only in events file, skip in movements
						continue;
                    case "99": // Disconnect: do not include in movements/raw file
						continue;
					default:
						item.Add("Event", "ERROR");
						break;
				}
				data.Add(item);
			}
			string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
			return json;
		}

		public string JsonifyEventsOnly()
		{
			List<Dictionary<string, object>> data = new();
			foreach (string str in EventLogging)
			{
				var parts = str.Split(" | ");
				if (parts.Length == 0) continue;
				if (parts[0].Trim() == "1") continue; // skip Pos events
				var item = new Dictionary<string, object>();
				switch (parts[0].Trim())
				{
					case "0":
						item.Add("Event", "StartGame");
						item.Add("Time", parts[1].Trim());
						break;
					case "2":
						item.Add("Event", "Task");
						if (TimeSpan.TryParse(parts[4].Trim(), out var t2)) { item.Add("Time", (GameStartedUTC + t2).ToString()); } else { item.Add("Time", parts[4].Trim()); }
						item.Add("Name", parts[1].Trim());
						item.Add("TaskName", parts[2].Trim());
						break;
					case "3":
						item.Add("Event", "Death");
						if (TimeSpan.TryParse(parts[4].Trim(), out var t3)) { item.Add("Time", (GameStartedUTC + t3).ToString()); } else { item.Add("Time", parts[4].Trim()); }
						item.Add("Name", parts[1].Trim());
						item.Add("Killer", parts[2].Trim());
						break;
					case "4":
						item.Add("Event", "EnterVent");
						if (TimeSpan.TryParse(parts[4].Trim(), out var t4)) { item.Add("Time", (GameStartedUTC + t4).ToString()); } else { item.Add("Time", parts[4].Trim()); }
						item.Add("Name", parts[1].Trim());
						item.Add("Vent", parts[2].Trim());
						break;
					case "5":
						item.Add("Event", "ExitVent");
						if (TimeSpan.TryParse(parts[4].Trim(), out var t5)) { item.Add("Time", (GameStartedUTC + t5).ToString()); } else { item.Add("Time", parts[4].Trim()); }
						item.Add("Name", parts[1].Trim());
						item.Add("Vent", parts[2].Trim());
						break;
					case "6":
						item.Add("Event", "BodyReport");
						item.Add("Player", parts[1].Trim());
						item.Add("DeadPlayer", parts[2].Trim());
						if (TimeSpan.TryParse(parts[4].Trim(), out var t6)) { item.Add("Time", (GameStartedUTC + t6).ToString()); } else { item.Add("Time", parts[4].Trim()); }
						break;
					case "7":
						item.Add("Event", "PlayerVote");
						if (TimeSpan.TryParse(parts[1].Trim(), out var t7)) { item.Add("Time", (GameStartedUTC + t7).ToString()); } else { item.Add("Time", parts[1].Trim()); }
						item.Add("Player", parts[2].Trim());
						item.Add("Target", parts[3].Trim());
						item.Add("Type", parts[4].Trim());
						break;
					case "8":
						item.Add("Event", "MeetingStart");
						if (TimeSpan.TryParse(parts[2].Trim(), out var t8)) { item.Add("Time", (GameStartedUTC + t8).ToString()); } else { item.Add("Time", parts[2].Trim()); }
						item.Add("Player", parts[1].Trim());
						break;
					case "9":
						item.Add("Event", "MeetingEnd");
						item.Add("Result", parts[1].Trim());
						if (TimeSpan.TryParse(parts[2].Trim(), out var t10)) { item.Add("Time", (GameStartedUTC + t10).ToString()); } else { item.Add("Time", parts[2].Trim()); }
						break;
					case "10":
						item.Add("Event", "EndGame");
						item.Add("WinReason", parts[1].Trim());
						if (TimeSpan.TryParse(parts[2].Trim(), out var t11)) { item.Add("Time", (GameStartedUTC + t11).ToString()); } else { item.Add("Time", parts[2].Trim()); }
						break;
					case "11":
						item.Add("Event", "Exiled");
						if (TimeSpan.TryParse(parts[2].Trim(), out var t12)) { item.Add("Time", (GameStartedUTC + t12).ToString()); } else { item.Add("Time", parts[2].Trim()); }
						item.Add("Player", parts[1].Trim());
						break;
					case "99":
						item.Add("Event", "Disconnect");
						if (TimeSpan.TryParse(parts[1].Trim(), out var t99)) { item.Add("Time", (GameStartedUTC + t99).ToString()); } else { item.Add("Time", parts[1].Trim()); }
						item.Add("Name", parts[2].Trim());
						break;
					default:
						item.Add("Event", "ERROR");
						break;
				}
				data.Add(item);
			}
			return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
		}
	}

	public class VoteData
	{
		public string Player { get; set; } = string.Empty;
		public string Target { get; set; } = string.Empty;
		public VoteType VoteType { get; set; }
		public TimeSpan Time { get; set; }
	}
}
