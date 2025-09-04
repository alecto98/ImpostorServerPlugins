using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchLog
{
    public class Config
    {

		public string seasonName { get; set; } = "Preseason";
		public bool enableReplay { get; set; } = true;
		public string outputPath { get; set; } = string.Empty; // If empty, defaults to plugins/MatchLogs/{seasonName}

    }
}
