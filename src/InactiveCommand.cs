using System;
using System.Collections.Generic;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{

	public class InactiveChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/inactive")) {
				return false;
				}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "inactive")) {
				return true;
			}
			int days;
			if (splits.Count == 2) {
				if (!int.TryParse(splits[1], out days)) {
					Chat.Send(causedBy, $"Could not parse days value");
					return true;
				}
			} else {
				Chat.Send(causedBy, "Syntax: /inactive {days}");
				return true;
			}

			Dictionary<Players.Player, int> inactivePlayers = ActivityTracker.GetInactivePlayers(days);
			List<Colony> inactiveColonies = new List<Colony>();
			int colonistCount = 0;
			foreach (Colony colony in ServerManager.ColonyTracker.ColoniesByID.Values) {
				bool activeOwnerFound = false;
				if (colony.Owners.Length == 0) {
					continue;
				}
				foreach (Players.Player owner in colony.Owners) {
					if (!inactivePlayers.ContainsKey(owner)) {
						activeOwnerFound = true;
					}
				}
				if (!activeOwnerFound) {
					inactiveColonies.Add(colony);
					colonistCount += colony.Followers.Count;
				}
			}

			string msg = $"No players inactive longer than {days} days";
			if (inactivePlayers.Count > 0)  {
				msg = String.Format("{0} players inactive since {1} days. Would purge {2} colonies with {3} colonists", inactivePlayers.Count, days, inactiveColonies.Count, colonistCount);
			};
			Chat.Send(causedBy, msg);
			return true;
		}
	}
}

