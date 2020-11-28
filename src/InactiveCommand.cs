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

			string resultMsg = "";
			int counter = 0;
			foreach (KeyValuePair<Players.Player, int> entry in ActivityTracker.GetInactivePlayers(days)) {
				Players.Player player = entry.Key;
				int inactiveDays = entry.Value;
				if (resultMsg.Length > 0) {
					resultMsg += ", ";
				}
				resultMsg += $"{player.ID.ToStringReadable()}({inactiveDays})";
				counter++;
			}
			if (counter == 0) {
				resultMsg = "No inactive players found";
			} else {
				resultMsg += $". In total {counter} players";
			}
			Chat.Send(causedBy, resultMsg);
			return true;
		}
	}
}

