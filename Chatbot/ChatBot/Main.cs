using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Configuration;
using System.IO;
using System.Threading;

using SteamKit2;

// Group chat bot. Based on the last example in SteamKit examples

namespace GroupChatBot
{
	class Program
	{

		public static Dictionary<SteamID, Chatter> friends { get; set; }
		public static Dictionary<string, SteamBot> bots;
		
		static void Main( string[] args )
		{
			friends = new Dictionary<SteamID, Chatter>();
			bots = new Dictionary<string, SteamBot> ();
			ConsoleLoop ();
			SaveUserPrefrences ();

		}

		// Input loop for adding bots, sending global messages, etc...
		public static void ConsoleLoop()
		{
			bool keeprunning = true;
			while (keeprunning)
			{
				// Wait for input
				Console.Write ("Group Chat Bot: ");
				string input = Console.ReadLine ();

				// Add another Steam instance
				if (input == "addbot")
				{
					// Get username and password
					Console.Write("Username: ");
					string username = Console.ReadLine ();

					Console.Write ("Password: ");
					string password = Console.ReadLine ();

					// Start me up...
					SteamBot newBot = new SteamBot(username, password);
					bots.Add (username, newBot);

					Thread botThread = new Thread(new ThreadStart(newBot.MainLoop));
					botThread.Start ();
				} else if (input == "killbot")
				{
					// Get username to kill...
					Console.Write ("Username: ");
					string username = Console.ReadLine ();

					bots[username].KillBot();

				} else if (input.StartsWith ("global "))
				{
					string toSend = input.Substring(7);
					SendGlobalMessage(toSend);
				} else if (input == "quit") {

					keeprunning = false;
					foreach (SteamBot bot in bots.Values)
					{
						bot.KillBot();
					}
				} else if (input.StartsWith ("kick "))
				{
					string str_target = input.Substring (5);
					SteamID target = new SteamID(str_target);
					friends[target].steamBot.steamFriends.RemoveFriend(target);
					friends.Remove(target);
				}


				Console.WriteLine ();
			}
		}

		public static void SendChannelMessage(string message, string channel, SteamID sender)
		{
			// The overall way to send messages to a channel
			foreach (SteamID steamID in friends.Keys) {
				if (steamID != sender && friends[steamID].active && friends[steamID].channel == channel) {
					friends[steamID].steamBot.steamFriends.SendChatMessage (steamID, EChatEntryType.ChatMsg, message);
				}
			}

			// Log the message...
			using (StreamWriter sw = File.AppendText("logs/" + channel + ".log"))
			{
				sw.WriteLine (message);
			}
		}

		public static string GetChannel (string channel)
		{
			Dictionary<string, string> channels = new Dictionary<string, string>();
			char[] splitter = new char[1];
			splitter[0] = '|';

			string[] lines = System.IO.File.ReadAllLines("channels.cfg");
			foreach ( string line in lines )
			{
				if (!line.StartsWith ("#")) {
					string[] channelAndUsers = line.Split(splitter);
					channels.Add(channelAndUsers[0], channelAndUsers[1]);
				}
			}
			try {
				return channels[channel];
			} catch (System.Collections.Generic.KeyNotFoundException) {
				return null;
			}
		}

		public static string GetConfig(string key)
		{
			Dictionary<string, string> channels = new Dictionary<string, string>();
			char[] splitter = new char[1];
			splitter[0] = '|';
			
			string[] lines = System.IO.File.ReadAllLines("settings.cfg");
			foreach ( string line in lines )
			{
				if (!line.StartsWith ("#")) {
					string[] channelAndUsers = line.Split(splitter);
					channels.Add(channelAndUsers[0], channelAndUsers[1]);
				}
			}
			try {
				return channels[key];
			} catch (System.Collections.Generic.KeyNotFoundException) {
				return null;
			}
		}

		// Gets what the user's channel used to be so that when the bot restarts they won't be taken
		// to the partyline.
		public static string GetOldChannel(string steamID)
		{
			// Loads up data from "settings.cfg" so that the bot is a little more customizable
			Dictionary<string, string> userAndChannel = new Dictionary<string, string>();
			char[] splitter = new char[1];
			splitter[0] = '|';
			
			string[] lines = System.IO.File.ReadAllLines("users.cfg");
			foreach ( string line in lines )
			{
				if (!line.StartsWith ("#")){
					string[] keyAndValue = line.Split(splitter);
					userAndChannel.Add(keyAndValue[0], keyAndValue[1]);
				}
			}
			try {
				return userAndChannel[steamID];
			} catch (System.Collections.Generic.KeyNotFoundException) {
				return "partyline";
			}
		}

		// Sends a message to everyone connected to the bot
		public static void SendGlobalMessage(string message)
		{
			foreach (SteamID steamID in friends.Keys)
			{
				friends[steamID].steamBot.steamFriends.SendChatMessage (steamID, EChatEntryType.ChatMsg, "** Global Message: " + message);
			}
		}

		// Function saves what channel the user was on before the bot quits.
		public static void SaveUserPrefrences()
		{
			string[] linesToWrite = new string[friends.Count+1];
			linesToWrite[0] = "# File for saving user preferences. DO NOT EDIT UNLESS YOU KNOW WHAT YOU'RE DOING.";
			int i = 1;
			foreach (SteamID steamID in friends.Keys)
			{
				linesToWrite[i] = steamID.ToString () + "|" + friends[steamID].channel;
				i++;
			}

			System.IO.File.WriteAllLines ("users.cfg", linesToWrite);
		}

		// Grabs logs when we want them.
		public static string[] GetHistory(string channel, int howManyLines)
		{
			if (howManyLines > 21) {
				return new string[1] { "Error, maximum allowed history is 20 lines." };
			} else {
				string[] lines = System.IO.File.ReadAllLines ("logs/" + channel + ".log");
				string[] toReturn = new string[howManyLines+2];
				toReturn[0] = "The past " + howManyLines.ToString() + " lines in " + channel;
				toReturn[howManyLines+1] = "End of log output.";

				Array.Copy(lines, lines.Length-howManyLines, toReturn, 1, howManyLines);

				return toReturn;
			}

		}

	}
}
