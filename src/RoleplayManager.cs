using System.Collections.Generic;

namespace ColonyCommands {

	// static class to hold roleplay info. Skeleton for now, will maybe get extended later
	public static class RoleplayManager
	{
		public static Dictionary<Players.Player, bool> Roleplayers = new Dictionary<Players.Player, bool>();

		// IsRoleplaying
		public static bool IsRoleplaying(Players.Player p)
		{
			return Roleplayers.ContainsKey(p);
		}

		// GetPlayers
		public static List<string> GetPlayers {
			get {
				List<string> result = new List<string>();
				foreach (Players.Player p in Roleplayers.Keys) {
					result.Add(p.Name);
				}
				return result;
			}
		}

		// AddPlayer
		public static bool AddPlayer(Players.Player p)
		{
			if (IsRoleplaying(p)) {
				return false;
			}
			Roleplayers.Add(p, true);
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
	}
}

