using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net;
using System.Collections.Generic;

namespace LogCleaner
{
	class Program
	{
		public enum AddressType
		{
			Server,
			Client
		}

		public static int Padding { get; set; } = 50;
		public static string ReplacementLog { get; set; } = "\nThe following replacements were made in the log file:\n";
		public static string LogText { get; set; }
		public static string ServerAddress { get; set; }

		public static int TokenCount { get; set; }
		public static int AddressCount { get; set; }
		public static List<string> Replaced { get; set; } = new List<string>();

		static void Main(string[] args)
		{
			if(args.Length < 1) {
				Console.WriteLine("Usage:");
				Console.WriteLine("LogCleaner.exe INPUT_LOG_FILENAME");
				return;
			}

			Configuration.Load();

			LogText = File.ReadAllText(args[0]);

			ServerAddress = Configuration.Get("ServerAddress");
			if(string.IsNullOrEmpty(ServerAddress)) {
				SetServerAddress();
			}

			Console.WriteLine("Server address is {0}", ServerAddress);

			// Clean server address
			Replace(ServerAddress, CleanNetworkAddress(ServerAddress, AddressType.Server), "server address");

			// Clean API keys from web sockets and login/logout messages
			RemoveByRegex(new Regex("api_key=([0-9a-f]{32})"), "access token");
			RemoveByRegex(new Regex("access token \"([0-9a-f]{32})\""), "access token");

			foreach(Match address in new Regex("(?:[0-9]{1,3}\\.){3}[0-9]{1,3}").Matches(LogText)) {
				var raw = address.Value;
				if(Replaced.Contains(raw)) { continue; }

				var replace = CleanNetworkAddress(raw, AddressType.Client);

				Replace(raw, replace, "IP address");
				Replaced.Add(raw);
			}

			Console.WriteLine(ReplacementLog);

			File.WriteAllText("cleaned.log", LogText);
		}

		public static void SetServerAddress()
		{
			Regex address = new Regex("https?://(.*?)(?::[0-9]+)?/");
			ServerAddress = address.Match(LogText).Groups[1].Value;
			Configuration.Set("ServerAddress", ServerAddress);

			Console.WriteLine("Auto detected server address as {0}", ServerAddress);
		}

		public static string CleanNetworkAddress(string input, AddressType addressType)
		{
			input = input.ToLower();
			if(input == "localhost" || input == "127.0.0.1" || input == "::1") {
				Console.WriteLine("INFO: Not cleaning loopback address {0}", input);
				return input;
			}

			IPAddress address;
			if(IPAddress.TryParse(input, out address) && IsInternal(address)) {
				Console.WriteLine("INFO: Not cleaning local address {0}", input);
				return input;
			}

			if(addressType == AddressType.Client) {
				return $"IP_ADDRESS_{++AddressCount}";
			}

			else if(addressType == AddressType.Server) {
				return "SERVER_ADDRESS";
			}

			Console.WriteLine($"WARN: No action defined for cleaning network address {input} with type {addressType}, not cleaning");
			return input;
		}

		public static void RemoveByRegex(Regex regex, string description, int group = 1)
		{
			var apiKeys = regex.Matches(LogText);
			foreach (Match raw in apiKeys) {
				var key = raw.Groups[group].Value;
				if (Replaced.Contains(key)) { continue; }

				Replace(key, CleanToken(), description);
				Replaced.Add(key);
			}
		}

		public static string CleanToken()
		{
			return $"ACCESS_TOKEN_{++TokenCount}";
		}

		public static bool IsInternal(IPAddress address)
		{
			byte[] ip = address.GetAddressBytes();
			switch (ip[0]) {
				case 10:
				case 127:
					return true;
				case 172:
					return ip[1] >= 16 && ip[1] < 32;
				case 192:
					return ip[1] == 168;
				default:
					return false;
			}
		}

		public static void Replace(string find, string replace, string description)
		{
			if(find == replace) {
				Console.WriteLine($"INFO: Find and replace for {description} had identical argument {find}, ignoring");
				return;
			}

			int matches = new Regex(Regex.Escape(find)).Matches(LogText).Count;
			LogText = LogText.Replace(find, replace);

			ReplacementLog += string.Format("{0} {1} {2} ({3} times)\n", description.PadRight(Padding / 2, ' '), find.PadRight(Padding, ' '), replace.PadRight(Padding / 2, ' '), matches);
		}
	}
}
