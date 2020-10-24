using System;
using System.Collections.Generic;
using Chatting;

namespace ColonyCommands {

	public static class WarManager
	{
		private struct warEntry {
			public long started;
			public long duration;

			public warEntry(long s, long d)
			{
				started = s;
				duration = d;
			}
		}

		private static Dictionary<Players.Player, warEntry> warDict = new Dictionary<Players.Player, warEntry>();

		// get player list
		public static List<string> PlayerList {
			get {
				List<string> result = new List<string>();
				foreach (Players.Player p in warDict.Keys) {
					string entry = p.Name + "[" + GetRemainingTimestring(warDict[p]) + "]";
					result.Add(entry);
				}
				return result;
			}
		}

		private static string GetRemainingTimestring(warEntry entry)
		{
			long minutes = (entry.started + entry.duration - DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000) / 60;
			return $"{minutes / 60}:{minutes % 60}";
		}

		// check if war enabled Player
		public static bool IsWarEnabled(Players.Player player)
		{
			return warDict.ContainsKey(player);
		}

		// check if war enabled Colony
		public static bool IsWarEnabled(Colony colony)
		{
			if (colony.Owners == null || colony.Owners.Length == 0) {
				return false;
			}
			bool result = false;
			foreach (Players.Player owner in colony.Owners) {
				if (IsWarEnabled(owner)) {
					result = true;
				}
			}

			return result;
		}

		// enable war for a player. Also used to reset the timestamp to now
		public static void EnableWar(Players.Player player, int duration)
		{
			if (duration < AntiGrief.WarDuration) {
				duration = AntiGrief.WarDuration;
			}
			warEntry entry = new warEntry(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000, duration);
			warDict[player] = entry;
		}

		// disable war for a player
		public static void DisableWar(Players.Player player)
		{
			warDict.Remove(player);

			if (player.ConnectionState == Players.EConnectionState.Connected) {
				Chat.Send(player, "<color=yellow>Your WAR status expired. Do no longer attack others!</color>");
			}
			Chat.SendToConnectedBut(player, "<color=yellow>WAR status of {player.Name} expired.</color>");
		}

		// end all wars
		public static void EndAllWars()
		{
			warDict.Clear();
		}

		// check war status and disable after timeout
		public static void CheckWarStatus()
		{
			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
			List<Players.Player> toDisable = new List<Players.Player>();

			foreach (KeyValuePair<Players.Player, warEntry> kvp in warDict) {
				Players.Player target = kvp.Key;
				warEntry entry = kvp.Value;
				if (entry.started + entry.duration <= now) {
					toDisable.Add(target);
				}
			}

			foreach (Players.Player target in toDisable) {
				DisableWar(target);
			}

			ThreadManager.InvokeOnMainThread(delegate() {
				CheckWarStatus();
			}, 65.350);

			return;
		}

	}
}

