using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Pipliz;
using Pipliz.JSON;
using Chatting;
using Shared.Networking;

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
		private static string warPermissionGroup = "peasant";
		private static Dictionary<Players.Player, List<string>> storedPermissions = new Dictionary<Players.Player, List<string>>();

		static string SaveFile {
			get {
				return Path.Combine(Path.Combine("gamedata", "savegames"), Path.Combine(ServerManager.WorldName, "war-permissions-to-restore.json"));
			}
		}


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
			// war is only allowed within roleplay setting
			if (!RoleplayManager.IsRoleplaying(player)) {
				return;
			}
			if (duration < AntiGrief.WarDuration) {
				duration = AntiGrief.WarDuration;
			}
			warEntry entry = new warEntry(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000, duration);
			warDict[player] = entry;

			// enable AngryGuards active mode for all colonies
			if (AntiGrief.AngryGuardsWarMode != null) {
				foreach (Colony colony in player.Colonies) {
					AntiGrief.AngryGuardsWarMode.Invoke(null, new object[]{colony, true});
				}
			}

			RemovePlayerPermissions(player);	// ensure fair wars
		}


		// disable war for a player
		public static void DisableWar(Players.Player player)
		{
			warDict.Remove(player);

			if (player.ConnectionState == Players.EConnectionState.Connected) {
				Chat.Send(player, "<color=yellow>Your WAR status expired. Do no longer attack others!</color>");
			}
			Chat.SendToConnectedBut(player, "<color=yellow>WAR status of {player.Name} expired.</color>");

			// disable AngryGuards active mode for all colonies
			if (AntiGrief.AngryGuardsWarMode != null) {
				foreach (Colony colony in player.Colonies) {
					AntiGrief.AngryGuardsWarMode.Invoke(null, new object[]{colony, false});
				}
			}

			RestorePlayerPermissions(player);
		}


		// remove player permissions and set to default group (fair wars)
		public static void RemovePlayerPermissions(Players.Player target)
		{
			// safe current groups of the player
			List<string> groups = new List<string>();
			PermissionsManager.PermissionsGroup pGroup;
			if (PermissionsManager.Users.TryGetValue(target.ID, out pGroup)) {
				try {
					List<string> playerGroups = pGroup.GetType().GetField("ParentGroups", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pGroup) as List<string>;
					groups.AddRange(playerGroups);
				} catch {
					groups.Add(warPermissionGroup);
				}
			}
			if (!groups[0].Equals(warPermissionGroup)) {
				storedPermissions[target] = groups;
				SavePermissions();
				PermissionsManager.SetGroupOfUser((Players.Player)null, target, warPermissionGroup);
				if (target.ConnectionState == Players.EConnectionState.Connected) {
					Chat.Send(target, $"<color=yellow>Permissions set to {warPermissionGroup}</color>");
				}
			}

			// remove flight state
			bool flightState = target.GetTempValues(false).GetOrDefault("pipliz.setflight", false);
			if (flightState) {
				// target.GetTempValues(true).Set("pipliz.setflight", false);
				target.GetTempValues(false).Remove("pipliz.setflight");
				target.ClearTempValuesIfEmpty();
				using (ByteBuilder byteBuilder = ByteBuilder.Get()) {
					byteBuilder.Write(ClientMessageType.SetFlight);
					byteBuilder.Write(false);
					NetworkWrapper.Send(byteBuilder, target, NetworkMessageReliability.ReliableWithBuffering);
				}
			}
		}


		// restore player permissions at end of war
		public static void RestorePlayerPermissions(Players.Player target)
		{
			if (!storedPermissions.ContainsKey(target)) {
				return;
			}

			for (int i = 0; i < storedPermissions[target].Count; i++) {
				string groupName = storedPermissions[target][i];
				if (i == 0) {
					PermissionsManager.SetGroupOfUser((Players.Player)null, target, groupName);
				} else {
					PermissionsManager.AddGroupToUser((Players.Player)null, target, groupName);
				}
				if (target.ConnectionState == Players.EConnectionState.Connected) {
					Chat.Send(target, $"<color=yellow>Restored group {groupName}</color>");
				}
				Log.Write($"Restored permission {groupName} for {target.Name}");
			}

			// delete the permissions file
			if (storedPermissions.Count == 0) {
				if (File.Exists(SaveFile)) {
					File.Delete(SaveFile);
					Log.Write("Deleted {0}, all permissions restored", SaveFile);
				}
			}
		}


		// end all wars
		public static void EndAllWars()
		{
			List<Players.Player> toDisable = new List<Players.Player>();
			foreach (KeyValuePair<Players.Player, warEntry> kvp in warDict) {
				Players.Player target = kvp.Key;
				toDisable.Add(target);
			}
			foreach (Players.Player target in toDisable) {
				DisableWar(target);
			}
			warDict.Clear();
		}


		// SAVE - store permissions as failsafe to allow restore always
		public static void SavePermissions()
		{
			if (storedPermissions.Count == 0) {
				return;
			}
			Log.Write("Saving player permission groups to {0}", SaveFile);

			JSONNode jsonFile = new JSONNode();
			JSONNode jsonPlayers = new JSONNode(NodeType.Array);
			foreach (KeyValuePair<Players.Player, List<string>> kvp in storedPermissions) {
				JSONNode jsonPlayerRecord = new JSONNode();
				Players.Player target = kvp.Key;
				jsonPlayerRecord.SetAs("SteamId", target.ID.steamID);
				jsonPlayerRecord.SetAs("Name", target.Name);

				JSONNode jsonRecords = new JSONNode(NodeType.Array);
				foreach (string groupName in kvp.Value) {
					JSONNode jsonRecord = new JSONNode();
					jsonRecord.SetAs<string>(groupName);
					jsonRecords.AddToArray(jsonRecord);
				}
				jsonPlayerRecord.SetAs("Groups", jsonRecords);
				jsonPlayers.AddToArray(jsonPlayerRecord);
			}
			jsonFile.SetAs("players", jsonPlayers);

			try {
				JSON.Serialize(SaveFile, jsonFile);
			} catch (Exception e) {
				Log.Write("Could not save {0}: {1}", SaveFile, e.Message);
			}
		}


		// LOAD permissions file
		public static void LoadPermissionsFile()
		{
			JSONNode jsonConfig;
			if (!JSON.Deserialize(SaveFile, out jsonConfig, false)) {
				Log.Write("Error reading {0}, please restore permissions manually", SaveFile);
				return;
			}

			Log.Write("Loading permissions to restore from {0}", SaveFile);
			try {
				JSONNode players;
				jsonConfig.TryGetAs("players", out players);
				foreach (JSONNode node in players.LoopArray()) {
					string steamId = node.GetAs<string>("SteamId");

					List<string> groups = new List<string>();
					JSONNode jsonGroups;
					node.TryGetAs("Groups", out jsonGroups);
					foreach (JSONNode gnode in jsonGroups.LoopArray()) {
						string grp = gnode.GetAs<string>();
						groups.Add(grp);
					}

					Players.Player target;
					string error;
					if (PlayerHelper.TryGetPlayer(steamId, out target, out error, true)) {
						storedPermissions[target] = groups;
					}
				}

			} catch (Exception e) {
				Log.Write("Error parsing {0}: {1}", SaveFile, e.Message);
			}
		}


		// LOAD - ensure all permissions are restored at server startup
		public static void Load()
		{
			if (File.Exists(SaveFile)) {
				LoadPermissionsFile();
				foreach (Players.Player target in storedPermissions.Keys) {
					RestorePlayerPermissions(target);
				}
			}
			CheckWarStatus();
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


	}	// end of class
} // namespace

