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
        public string gameStarted { get; set; }
        public string players { get; set; }
        public string impostors { get; set; }
        public string eventsLogFile { get; set; }
        public string result { get; set; }
        public string reason { get; set; }

    }
}