using System;

using SteamKit2;

namespace GroupChatBot
{
	public class Chatter
	{
		public SteamID steamID { get; set; }

		public bool active { get; set; }

		public String channel { get; set; }

		public EPersonaState steamStatus { get; set; }

		public SteamBot steamBot;

		public bool addedToList;

		public Chatter (SteamID sid, SteamBot sBot, string chan)
		{
			steamID = sid;
			active = true;
			channel = chan;
			steamBot = sBot;
			addedToList = false;
		}

		public void toggle()
		{
			active = !active;
		}

		public override string ToString()
		{
			return steamID.ToString();
		}


	}
}

