using Impostor.Api.Innersloth.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockedGameSettingsPlugin
{
    public class GameData
    {

        public NormalGameOptions gameOptions { get; set; }

        public GameData(NormalGameOptions options) {
            gameOptions = options;
        }

        public void setOptions(NormalGameOptions options) {
            gameOptions = options;
        }

    }
}
