using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LockedGameSettingsPlugin
{
    public sealed class RoleValues
    {
        private string jsonString = @"{
	        ""engineerCoolDown"": 25,
	        ""engineerInVentMaxTime"": 5,
	        ""engineerCount"": 1,
	        ""engineerChance"": 20,
	        ""scientistCooldown"": 25,
	        ""scientistBatteryCharge"": 3,
	        ""scientistCount"": 1,
	        ""scientistChance"": 20,
	        ""GACooldown"": 0,
	        ""GACount"": 0,
	        ""GAChance"": 0,
	        ""shapeshifterCooldown"": 25,
	        ""shapeshifterDuration"": 10,
	        ""shapeshifterLeaveSkin"": true,
	        ""shapeshifterCount"": 1,
	        ""shapeshifterChance"": 20
        }";
        //Engineer
        public int engineerCoolDown { get; set; } = 0;
        public int engineerInVentMaxTime { get; set; } = 0;
        public int engineerCount { get; set; } = 0;
        public int engineerChance { get; set; } = 0;
        //Scientist
        public int scientistCooldown { get; set; } = 0;
        public int scientistBatteryCharge { get; set; } = 0;
        public int scientistCount { get; set; } = 0;
        public int scientistChance { get; set; } = 0;
        //Guardian Angel
        public int GACooldown { get; set; } = 0;
        public int GACount { get; set; } = 0;
        public int GAChance { get; set; } = 0;
        //Shapeshifter
        public int shapeshifterCooldown { get; set; } = 0;
        public int shapeshifterDuration { get; set; } = 0;
        public bool shapeshifterLeaveSkin { get; set; } = true;
        public int shapeshifterCount { get; set; } = 0;
        public int shapeshifterChance { get; set; } = 0;

        public RoleValues() 
        {
        }

        public RoleValues(RoleValues values)
        {
            //RoleValues values = JsonSerializer.Deserialize<RoleValues>(data);
            engineerCoolDown = values.engineerCoolDown;
            engineerInVentMaxTime = values.engineerInVentMaxTime;
            engineerCount = values.engineerCount;
            engineerChance = values.engineerChance;

            scientistCooldown = values.scientistCooldown;
            scientistBatteryCharge = values.scientistBatteryCharge;
            scientistCount = values.scientistCount;
            scientistChance = values.scientistChance;

            GACooldown = values.GACooldown;
            GACount = values.GACount;
            GAChance = values.GAChance;

            shapeshifterCooldown = values.shapeshifterCooldown;
            shapeshifterDuration = values.shapeshifterDuration;
            shapeshifterLeaveSkin = values.shapeshifterLeaveSkin;
            shapeshifterCount = values.shapeshifterCount;
            shapeshifterChance = values.shapeshifterChance;
        }
    }
}
