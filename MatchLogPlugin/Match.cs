using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MatchLog
{
    public class Match
    {

		public int MatchID { get; set; }
		public string GameStarted { get; set; } = string.Empty;
		public string Players { get; set; } = string.Empty;
		public string Colors { get; set; } = string.Empty;
		public string Impostors { get; set; } = string.Empty;
		public string eventsLogFile { get; set; } = string.Empty;
		public string MovementsFile { get; set; } = string.Empty;
		public string Result { get; set; } = string.Empty;
		public string Reason { get; set; } = string.Empty;

    }
}