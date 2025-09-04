using System.Collections.Generic;

namespace ChatHandlerPlugin
{
    public class Config
    {
        public string helpMessage { get; set; } = @"
Available commands:
/help or ?help - Show this help message
/rules or ?rules - Display server rules
/timer or ?timer - Show match timer information
/tasks or ?tasks - View remaining tasks (dead players only)
/skin - Change your skin to Witch
/cancel or ?cancel - Information about cancelling a match

For more information on a specific command, type '/help <command>' (e.g., '/help tasks')

Impostor chat commands:
// or ?? - Quick impostor chat
/say or ?say - Impostor chat
/w or ?w - Whisper (impostor chat)

Type '/help impostor' for more information on impostor chat.";
        public string rulesMessage { get; set; } = "Use the Discord Bot to check the rules.";
        public string timerMessage { get; set; } = "Type /timer or ?timer to see the timer during a match.";
        public string AnnouncementMessage { get; set; } = "Welcome to AU++ Ranked Server";
        public string wrongCommandMessage { get; set; } = "Wrong Command, please use one of the following commands: /timer | /help | /rules | /say | /skin";
        public string cancelMessage { get; set; } = "The host of the game can cancel a match at any point with the /cancel or ?cancel command followed up by a /end or ?end";
        public string impChatMessage { get; set; } = @"
Impostor Chat Commands:
// or ?? - Quick impostor chat (e.g., '//sus red')
/say or ?say - Impostor chat (e.g., '/say I think it's blue')
/w or ?w - Whisper (impostor chat) (e.g., '/w let's kill red')

Your partners' messages will display in Red. Only impostors and dead players can see these messages.";
        public List<string> ImpostorChatCommands { get; set; } = new List<string> { "//", "??", "/w", "?w", "/say", "?say" };
        public List<string> AllowedCommands { get; set; } = new List<string>
        {
            "/help", "?help", "/rules", "?rules", "/timer", "?timer", "/cancel", "?cancel",
            "/task", "?task", "/tasks", "?tasks", "?end", "/end", "/skin"
        };
        public string tasksMessage { get; set; } = "Only dead players can use the /tasks command to see remaining tasks.";
    }
}