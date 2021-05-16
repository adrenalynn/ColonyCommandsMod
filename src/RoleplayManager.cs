using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;

namespace ColonyCommands {

	// static class to hold roleplay info. Skeleton for now, will maybe get extended later
	public static class RoleplayManager
	{

		const string CONFIG_FILE = "roleplay-banned.json";
		private static Dictionary<Players.Player, bool> Roleplayers = new Dictionary<Players.Player, bool>();
		private static Dictionary<Players.Player, RoleplayRecord> BannedPlayers = new Dictionary<Players.Player, RoleplayRecord>();
		private static string ConfigFile { get {
			return Path.Combine(Path.Combine("gamedata", "savegames"), Path.Combine(ServerManager.WorldName, CONFIG_FILE)); }
		}


		// IsRoleplaying
		public static bool IsRoleplaying(Players.Player p)
		{
			return Roleplayers.ContainsKey(p);
		}


		// IsBanned
		public static bool IsRoleplayBanned(Players.Player p)
		{
			return BannedPlayers.ContainsKey(p);
		}


		// GetRoleplayers (for chat output)
		public static List<string> GetRoleplayers {
			get {
				List<string> result = new List<string>();
				foreach (Players.Player p in Roleplayers.Keys) {
					result.Add(p.Name);
				}
				return result;
			}
		}


		// GetRoleplayBanned (for chat output)
		public static List<Players.Player> GetRoleplayBanned
		{
			get {
				List<Players.Player> result = new List<Players.Player>();
				foreach (Players.Player p in BannedPlayers.Keys) {
					result.Add(p);
				}
				return result;
			}
		}

		// get banned record
		public static RoleplayRecord GetPlayerRecord(Players.Player p)
		{
			if (IsRoleplayBanned(p)) {
				return BannedPlayers[p];
			}
			return null;
		}


		// AddPlayer
		public static bool AddPlayer(Players.Player p)
		{
			if (IsRoleplayBanned(p)) {
				return false;
			}
			if (!IsRoleplaying(p)) {
				Roleplayers.Add(p, true);
			}
			return true;
		}


		// RemovePlayer
		public static bool RemovePlayer(Players.Player p)
		{
			if (!IsRoleplaying(p)) {
				return false;
			}
			Roleplayers.Remove(p);
			return true;
		}


		// Ban player from roleplay
		public static void BanPlayer(Players.Player causedBy, Players.Player target, long duration, string reason)
		{
			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
			RoleplayRecord record = new RoleplayRecord(now, duration, target, reason);
			RemovePlayer(target);
			BannedPlayers.Add(target, record);
			if (WarManager.IsWarEnabled(target)) {
				WarManager.DisableWar(target);
			}
			Log.Write($"{causedBy.Name} banned {target.Name}/{target.ID.steamID} from roleplaying: {reason}");
			Save();
		}


		// Unban player from roleplay
		public static bool UnbanPlayer(Players.Player target)
		{
			if (!IsRoleplayBanned(target)) {
				return false;
			}
			BannedPlayers.Remove(target);
			Save();
			return true;
		}


		// save banned records
		public static void Save()
		{
			// if no records but file exists: delete it
			if (BannedPlayers.Count == 0 && File.Exists(ConfigFile)) {
				File.Delete(ConfigFile);
				return;
			}

			Log.Write("Saving roleplay config to {0}", CONFIG_FILE);

			JSONNode records = new JSONNode(NodeType.Array);
			foreach (KeyValuePair<Players.Player, RoleplayRecord> kvp in BannedPlayers) {
				JSONNode entry = new JSONNode(NodeType.Object)
					.SetAs("player", kvp.Key.ID.steamID)
					.SetAs("record", kvp.Value.ToJSON())
				;
				records.AddToArray(entry);
			}

			try {
				JSON.Serialize(ConfigFile, records);
			} catch (Exception e) {
				Log.Write("Error saving {0}: {1}", ConfigFile, e.Message);
			}
		}


		// load records
		public static void Load()
		{
			JSONNode playerRecords;
			if (!JSON.Deserialize(ConfigFile, out playerRecords, false)) {
				return;
			}

			Log.Write("Loading roleplay banned players from {0}", ConfigFile);
			try {

				foreach (JSONNode node in playerRecords.LoopArray()) {
					string error;
					Players.Player target;
					PlayerHelper.TryGetPlayer(node.GetAs<string>("player"), out target, out error);
					if (target == null) {
						continue;
					}
					RoleplayRecord record = new RoleplayRecord(node["record"]);
					BannedPlayers.Add(target, record);
				}

			} catch (Exception e) {
				Log.Write("Error parsing {0}: {1}", CONFIG_FILE, e.Message);
			}
			CheckAndReleasePlayers();
		}


		// check time/duration and release players
		public static void CheckAndReleasePlayers()
		{
			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
			List<Players.Player> toRelease = new List<Players.Player>();

			foreach (KeyValuePair<Players.Player, RoleplayRecord> kvp in BannedPlayers) {
				if (kvp.Value.timestamp + kvp.Value.duration <= now) {
					toRelease.Add(kvp.Key);
				}
			}

			foreach (Players.Player target in toRelease) {
				BannedPlayers.Remove(target);
			}
			Save();

			ThreadManager.InvokeOnMainThread(delegate { CheckAndReleasePlayers(); }, 300.0f);
		}


		// return nicely formatted times
		public static string prettyPrintDuration(long timevalue)
		{
			int days = (int)(timevalue / (60 * 60 * 24));
			int hours = (int)(timevalue % (60 * 60 * 24) / (60 * 60));
			int mins = (int)(timevalue / 60 % 60);
			int secs = (int)(timevalue % 60);
			if (days > 0) {
				return String.Format("{0}d {1:00}h:{2:00}m:{3:00}s", days, hours, mins, secs);
			} else if (hours > 0) {
				return String.Format("{0}h {1:00}m:{2:00}s", hours, mins, secs);
			}
			return String.Format("{0:00}m:{1:00}s", mins, secs);
		}


	} // RoleplayManager class


	// RoleplayRecord (for now just contains rp-banned player)
	public class RoleplayRecord {
		public long timestamp;
		public long duration;
		public Players.Player causedBy;
		public string reason;


		public RoleplayRecord(long t, long d, Players.Player p, string r)
		{
			this.timestamp = t;
			this.duration = d;
			this.causedBy = p;
			this.reason = r;
		}


		public RoleplayRecord(JSONNode record)
		{
			this.timestamp = record.GetAs<long>("timestamp");
			this.duration = record.GetAs<long>("duration");
			this.reason = record.GetAs<string>("reason");
			string error;
			Players.Player target;
			PlayerHelper.TryGetPlayer(record.GetAs<string>("causedby"), out target, out error, true);
			this.causedBy = target;
		}


		public JSONNode ToJSON()
		{
			JSONNode record = new JSONNode(NodeType.Object)
				.SetAs("timestamp", timestamp)
				.SetAs("duration", duration)
				.SetAs("causedby", causedBy.ID.steamID)
				.SetAs("reason", reason)
			;
			return record;
		}

	} // RoleplayRecord

}

