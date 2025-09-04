using Impostor.Api.Net;
using System.Collections.Generic;

namespace ChatHandlerPlugin
{
    public class GameData
    {
        public List<IClientPlayer> Players { get; set; }
        public List<IClientPlayer> Impostors { get; set; }

        public GameData()
        {
            Players = new List<IClientPlayer>();
            Impostors = new List<IClientPlayer>();
        }

        public void AddPlayer(IClientPlayer player, bool isImpostor)
        {
            Players.Add(player);
            if (isImpostor)
            {
                Impostors.Add(player);
            }
        }

        public void RemovePlayer(IClientPlayer player)
        {
            Players.Remove(player);
            Impostors.Remove(player);
        }

        public void ResetGame()
        {
            Players.Clear();
            Impostors.Clear();
        }
    }
}