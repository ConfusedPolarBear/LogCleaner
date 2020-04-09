using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LogCleaner
{
	public static class Configuration
	{
		private static Dictionary<string, string> values = new Dictionary<string, string>();

		public static void Load()
		{
			if(!File.Exists("config.json")) {
				File.WriteAllText("config.json", "{}");
			}

			string json = File.ReadAllText("config.json");
			values = (Dictionary<string, string>)JsonSerializer.Deserialize(json, values.GetType());
		}

		public static void Save()
		{
			string json = JsonSerializer.Serialize(values, values.GetType(), new JsonSerializerOptions()
			{
				WriteIndented = true
			});

			File.WriteAllText("config.json", json);
		}

		public static string Get(string key, string defaultValue = "")
		{
			if(!values.ContainsKey(key)) {
				Set(key, defaultValue);
			}

			return values[key];
		}

		public static void Set(string key, string value)
		{
			values[key] = value;
			Save();
		}
	}
}
