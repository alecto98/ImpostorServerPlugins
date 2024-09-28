using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Replay
{
    public class Match
    {
        public int MatchID { get; set; }
        public string GameStarted { get; set; }
        public string Players { get; set; }
        public string Colors { get; set; }
        public string Impostors { get; set; }
        public string MovementsFile { get; set; }
        public string Result { get; set; }
        public string Reason { get; set; }

        public void Process()
        {
            string directoryPath = Config.GetConfigValue("directoryPath");

            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = Path.Combine(Environment.CurrentDirectory, "plugins", "MatchLogs");
            }

            string matchPattern = "*_match.json";
            string[] matchFiles = Directory.GetFiles(directoryPath, matchPattern);

            string output_file = Path.Combine(directoryPath, "matches.txt");
            string error_file = Path.Combine(directoryPath, "errors.txt");
            StringBuilder output = new StringBuilder();

            try
            {
                foreach (string file in matchFiles)
                {
                    string jsonString = File.ReadAllText(file);
                    JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                    string minified = MinifyJson(jsonDocument.RootElement);
                    output.AppendLine(minified);
                }

                File.WriteAllText(output_file, output.ToString());
            }
            catch (Exception ex)
            {
                File.WriteAllText(error_file, ex.Message);
            }
        }

        public string MinifyJson(JsonElement jsonElement)
        {
            StringBuilder builder = new StringBuilder();
            WriteJsonElement(builder, jsonElement);
            return builder.ToString();
        }

        public void WriteJsonElement(StringBuilder builder, JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    bool first = true;
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        if (!first) builder.Append(',');
                        builder.Append('"').Append(property.Name).Append('"').Append(':');
                        WriteJsonElement(builder, property.Value);
                        first = false;
                    }
                    builder.Append('}');
                    break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    first = true;
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        if (!first) builder.Append(',');
                        WriteJsonElement(builder, item);
                        first = false;
                    }
                    builder.Append(']');
                    break;
                case JsonValueKind.String:
                    builder.Append('"').Append(jsonElement.GetString()).Append('"');
                    break;
                case JsonValueKind.Number:
                    builder.Append(jsonElement.ToString());
                    break;
                case JsonValueKind.True:
                    builder.Append("true");
                    break;
                case JsonValueKind.False:
                    builder.Append("false");
                    break;
                case JsonValueKind.Null:
                    builder.Append("null");
                    break;
                default:
                    throw new InvalidOperationException("Invalid JSON value kind.");
            }
        }
    }
}
