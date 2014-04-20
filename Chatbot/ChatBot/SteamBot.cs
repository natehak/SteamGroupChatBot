using System;
using System.Collections.Generic;
using System.Collections;

using SteamKit2;

namespace GroupChatBot
{
	public class SteamBot
	{
		SteamClient steamClient;
		CallbackManager manager;
		
		SteamUser steamUser;
		public SteamFriends steamFriends { get; set; }
		
		bool isRunning;
		
		string user, pass;

		public SteamBot (string newUser, string newPass)
		{
			// Bot user and password
			user = newUser;
			pass = newPass;

			// create our steamclient instance
			steamClient = new SteamClient( System.Net.Sockets.ProtocolType.Tcp );
			// create the callback manager which will route callbacks to function calls
			manager = new CallbackManager( steamClient );

			// get the steamuser handler, which is used for logging on after successfully connecting
			steamUser = steamClient.GetHandler<SteamUser>();
			// get the steam friends handler, which is used for interacting with friends on the network after logging on
			steamFriends = steamClient.GetHandler<SteamFriends>();

			// register a few callbacks we're interested in
			// these are registered upon creation to a callback manager, which will then route the callbacks
			// to the functions specified
			new Callback<SteamClient.ConnectedCallback>( OnConnected, manager );
			new Callback<SteamClient.DisconnectedCallback>( OnDisconnected, manager );
			
			new Callback<SteamUser.LoggedOnCallback>( OnLoggedOn, manager );
			
			// we use the following callbacks for friends related activities
			new Callback<SteamUser.AccountInfoCallback>( OnAccountInfo, manager );
			new Callback<SteamFriends.FriendsListCallback>( OnFriendsList, manager );
			new Callback<SteamFriends.FriendMsgCallback> (OnFriendMessage, manager);

			// initiate the connection
			steamClient.Connect();

			// Make sure the main loop runs
			isRunning = true;

		}

		public void MainLoop()
		{
			// create our callback handling loop
			while ( isRunning )
			{
				// in order for the callbacks to get routed, they need to be handled by the manager
				manager.RunWaitCallbacks( TimeSpan.FromSeconds( 1 ) );
			}

			steamClient.Disconnect ();
		}

		// Kill the bot!
		public void KillBot()
		{
			isRunning = false;
		}

		// Give status messages about the channel
		
		public void GiveStatusMessage(SteamID sender)
		{
			steamFriends.SendChatMessage(sender, EChatEntryType.ChatMsg, "** You are currently in the " + Program.friends[sender].channel + " channel.");
			if (Program.friends[sender].active)
			{
				steamFriends.SendChatMessage(sender, EChatEntryType.ChatMsg, "** You are set to recieve messages.");
			} else {
				steamFriends.SendChatMessage(sender, EChatEntryType.ChatMsg, "** You are set to ignore messages.");
			}
			string people = "";
			
			foreach (SteamID steamID in Program.friends.Keys)
			{
				if (Program.friends[steamID].channel == Program.friends[sender].channel && Program.friends[steamID].steamBot.steamFriends.GetFriendPersonaState(steamID) != EPersonaState.Offline)
				{
					people = people + Program.friends[steamID].steamBot.steamFriends.GetFriendPersonaName(steamID) + " (" + steamID + "), ";
				}
			}
			
			steamFriends.SendChatMessage (sender, EChatEntryType.ChatMsg, "** Currently in the channel is: " + people);
		}

		// Steam methods for bot calling

		// Gets called on first contact.
		private void OnConnected( SteamClient.ConnectedCallback callback )
		{
			// If it can't connect, wait and reconnect
			if ( callback.Result != EResult.OK )
			{
				Console.WriteLine( "Unable to connect to Steam: {0}", callback.Result );
				System.Threading.Thread.Sleep (5000);
				steamClient.Connect();
			} else {

				// Otherwise login...
				Console.WriteLine( "Connected to Steam! Logging in '{0}'...", user );
				
				steamUser.LogOn( new SteamUser.LogOnDetails
				                {
					Username = user,
					Password = pass,
				} );
			}
		}

		// Gets called when the bot disconnects, we immediately try reconnecting...
		private void OnDisconnected( SteamClient.DisconnectedCallback callback )
		{
			Console.WriteLine( "Connection failed." );
			steamClient.Connect();
		}

		// When the connect succeeds this gets called. We're going to try to login.
		private void OnLoggedOn( SteamUser.LoggedOnCallback callback )
		{
			// if the login failed, wait then try again.
			if ( callback.Result != EResult.OK )
			{
				Console.WriteLine( "Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult );
				System.Threading.Thread.Sleep (5000);
				steamUser.LogOn (new SteamUser.LogOnDetails {
					Username = user,
					Password = pass,
				});
			} else {
				Console.WriteLine( "Successfully logged on!" );
			}
			
			// at this point, we'd be able to perform actions on Steam
		}

		// When Steam asks for info we're going to log ourselves onto Steam Friends
		private void OnAccountInfo( SteamUser.AccountInfoCallback callback )
		{
			// before being able to interact with friends, you must wait for the account info callback
			// this callback is posted shortly after a successful logon
			
			// at this point, we can go online on friends, so lets do that
			steamFriends.SetPersonaName(Program.GetConfig("botname"));
			steamFriends.SetPersonaState( EPersonaState.Online );
		}

		// When the friends list gets updated we need to do things such as add the person back.
		private void OnFriendsList( SteamFriends.FriendsListCallback callback )
		{
			// at this point, the client has receivedh it's friends list
			
			int friendCount = steamFriends.GetFriendCount();
			
			for ( int x = 0 ; x < friendCount ; x++ )
			{
				// Cycle through the friends list, if there's anybody we don't know we're going to keep a tab on him
				SteamID steamIdFriend = steamFriends.GetFriendByIndex( x );
				if ( !Program.friends.ContainsKey(steamIdFriend) ) { Program.friends.Add(steamIdFriend, new Chatter(steamIdFriend, this, Program.GetOldChannel(steamIdFriend.ToString()))); }

				
			}
			
			// we can also iterate over our friendslist to accept or decline any pending invites
			
			foreach ( var friend in callback.FriendList )
			{
				try {
					if (friend.Relationship == EFriendRelationship.Friend)
					{
						Program.friends[friend.SteamID].addedToList = true;
					} else if (friend.Relationship == EFriendRelationship.RequestRecipient)
					{
						if (Program.friends[friend.SteamID].addedToList)
						{
							steamFriends.IgnoreFriend(friend.SteamID);
						} else {
							// this user has added us, let's add him back
							steamFriends.AddFriend(friend.SteamID);
							Program.friends[friend.SteamID].addedToList = true;
						}
					}
				} catch (System.Collections.Generic.KeyNotFoundException) {

				}
			}
		}

		private void OnFriendMessage( SteamFriends.FriendMsgCallback callback)
		{
			if (callback.EntryType == EChatEntryType.ChatMsg)
			{
				// Manage commands
				if (callback.Message.StartsWith ("/")) {

					// Find the actual command sent
					char[] cid = new char[1];
					cid[0] = '/';
					string command = callback.Message.TrimStart(cid);

					if (command.StartsWith("o"))
					// Disable or enable individual relays
					{
						Program.friends[callback.Sender].toggle();
						if ( Program.friends[callback.Sender].active )
						{
							steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Chat enabled.");
						} else {
							steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Chat disabled.");
						}
					} else if (command.StartsWith("j"))
					{
						// Manages switching of channels
						try {

							// Get the channel requested
							char[] splitter = new char[1];
							splitter[0] = ' ';

							// Get the channel's information
							string[] args = command.Split(splitter);
							string userlist = Program.GetChannel(args[1]);
							
							if (userlist == null) {
								steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Error, not a channel.");
							} else if (userlist.Equals("free"))
							{
								Program.SendChannelMessage("** " + steamFriends.GetFriendPersonaName(callback.Sender) + " is leaving this channel.", Program.friends[callback.Sender].channel, callback.Sender);
								Program.SendChannelMessage("** " + steamFriends.GetFriendPersonaName (callback.Sender) + " has joined the channel.", args[1], callback.Sender);
								Program.friends[callback.Sender].channel = args[1];
								steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Switched to channel " + args[1]);
								GiveStatusMessage (callback.Sender);
								
							} else {
								
								splitter[0] = ',';
								List<string> users = new List<string>(userlist.Split(splitter));
								
								if (users.Contains(callback.Sender.ToString()))
								{
									Program.SendChannelMessage("** " + steamFriends.GetFriendPersonaName(callback.Sender) + " is leaving this channel.", Program.friends[callback.Sender].channel, callback.Sender);
									Program.SendChannelMessage("** " + steamFriends.GetFriendPersonaName (callback.Sender) + " has joined the channel.", args[1], callback.Sender);
									Program.friends[callback.Sender].channel = args[1];
									steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Switched to channel " + args[1]);
									GiveStatusMessage (callback.Sender);
									
								} else {
									steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: Unauthorized to join this channel.");
								}
							}
						} catch (System.IndexOutOfRangeException)
						{
							steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Notice: No channel specified.");
						}
					} else if (command.StartsWith("s"))
					{
						// Give a status message
						GiveStatusMessage(callback.Sender);
					} else if (command.StartsWith("l"))
					{
						// Give a list of channels
						string channellist = "";
						char[] splitter = new char[1];
						splitter[0] = '|';
						
						string[] lines = System.IO.File.ReadAllLines("channels.cfg");
						foreach ( string line in lines )
						{
							if (!line.StartsWith ("#"))
							{
								string[] channelAndUsers = line.Split(splitter);
								channellist = channellist + channelAndUsers[0] + ", ";
							}
						}
						
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** Official channels are: " + channellist);
						
					} else if (command.StartsWith("he"))
					{
						// Give help mesages
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** Commands are: ");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /join (or /j) - Changes what channel you are on.");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /on | /off (or just /o) - Toggles between receiving messages and ignoring messages");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /status (or /s) - Gives you information about what channel you're in, and whether you are receiving messages or not.");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /list (or /l) - Lists all the official channels.");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /help - Gives you this list of commands.");
						steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** /history i - Gives you the last i messages sent in the channel.");
					} else if (command.StartsWith("g") && callback.Sender.ConvertToUInt64() == Convert.ToUInt64(Program.GetConfig("adminid")))
					{
						// Global messages
						try {
							string toSend = callback.Message.Substring(3);

							Program.SendGlobalMessage (toSend);
						} catch (System.ArgumentOutOfRangeException) {
							
						}
					} else if (command.StartsWith ("hi"))
					{
						try {
							// Get the logs...
							char[] splitter = new char[1] { ' ' };
							string[] args = callback.Message.Split (splitter);

							string[] toSend = Program.GetHistory(Program.friends[callback.Sender].channel, Convert.ToInt32(args[1]));

							foreach (string line in toSend)
							{
								steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** " + line);
							}

						} catch (System.IndexOutOfRangeException) {
							steamFriends.SendChatMessage (callback.Sender, EChatEntryType.ChatMsg, "** Error, how many messages do you want?");
						} catch (System.FormatException) {
							steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "** Error, invalid number.");
						}

					}
				} else if ( Program.friends[callback.Sender].active ) {

					// If it isn't a command send a message
					string channel = Program.friends[callback.Sender].channel;
					string inGame = "";

					Console.Write(steamFriends.GetFriendGamePlayed(callback.Sender).AppID);

					if (steamFriends.GetFriendGamePlayed(callback.Sender).AppID != 0)
					{
						inGame = " [G] ";
					}
					Program.SendChannelMessage(steamFriends.GetFriendPersonaName(callback.Sender) + inGame + ": " + callback.Message, channel, callback.Sender);
					
				}
			}
		}


	}
}

