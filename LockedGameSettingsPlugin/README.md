# LockedGameSettings
Impostor Server Plugin for Ranked Among Us that Locks Lobby Settings to specific assigned values.


**Default config_lobby.json:**
```
{
  "MaxPlayers": 10,
  "Keywords": "English",
  "Map": "Polus",
  "NumImpostors": 2,
  "IsDefaults": false,
  "PlayerSpeedMod": 1.2,
  "CrewLightMod": 0.3,
  "ImpostorLightMod": 1,
  "KillCooldown": 22.5,
  "NumCommonTasks": 2,
  "NumLongTasks": 3,
  "NumShortTasks": 5,
  "NumEmergencyMeetings": 1,
  "EmergencyCooldown": 20,
  "GhostsDoTasks": true,
  "KillDistance": "Short",
  "DiscussionTime": 15,
  "VotingTime": 120,
  "ConfirmImpostor": false,
  "VisualTasks": false,
  "AnonymousVotes": true,
  "TaskBarUpdate": "Never",
  "RoleOptions": {
    "Version": 7,
    "Roles": {}

```

**Default config_roles.json:**
```
{
  "engineerCoolDown": 25,
  "engineerInVentMaxTime": 5,
  "engineerCount": 1,
  "engineerChance": 20,
  "scientistCooldown": 25,
  "scientistBatteryCharge": 5,
  "scientistCount": 1,
  "scientistChance": 20,
  "GACooldown": 0,
  "GACount": 0,
  "GAChance": 0,
  "shapeshifterCooldown": 25,
  "shapeshifterDuration": 10,
  "shapeshifterLeaveSkin": true,
  "shapeshifterCount": 1,
  "shapeshifterChance": 20
}

```

**Features:**
- Lobby Settings are Locked to specific values.
- Lobby Settings are able to be changed completely from Default Among Us values.
- Roles Settings are Locked to specific values.
