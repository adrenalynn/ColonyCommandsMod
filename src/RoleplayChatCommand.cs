using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

	public class RoleplayChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/rp")) {
				return false;
			}

			// ON
			if (splits.Count >= 2 && splits[1].Equals("on")) {
				if (RoleplayManager.AddPlayer(causedBy)) {
					Chat.Send(causedBy, "Enabled roleplay marking");
				} else {
					RoleplayRecord record = RoleplayManager.GetPlayerRecord(causedBy);
					long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
					string timeval = RoleplayManager.prettyPrintDuration(record.timestamp + record.duration - now);
					Chat.Send(causedBy, $"Banned for {timeval} from roleplaying! reason: {record.reason}");
				}

			// OFF
			} else if (splits.Count >= 2 && splits[1].Equals("off")) {
				if (RoleplayManager.RemovePlayer(causedBy)) {
					Chat.Send(causedBy, "Disabled roleplay marking");
				} else {
					Chat.Send(causedBy, "Roleplay marking was not active");
				}

			// LIST banned
			} else if (splits.Count >= 2 && splits[1].Equals("banned")) {
				List<Players.Player> players = RoleplayManager.GetRoleplayBanned;
				if (players.Count == 0) {
					Chat.Send(causedBy, "No roleplay banned players currently");
				} else {
					foreach (Players.Player target in players) {
						RoleplayRecord record = RoleplayManager.GetPlayerRecord(target);
						long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
						Chat.Send(causedBy, String.Format("{0} {1} by {2} for: {3}",
							target.Name,
							RoleplayManager.prettyPrintDuration(record.timestamp + record.duration - now),
							record.causedBy.Name,
							record.reason)
						);
					}
				}
			// LIST
			} else {
				List<string> players = RoleplayManager.GetRoleplayers;
				if (players.Count == 0) {
					Chat.Send(causedBy, "<color=yellow>Currently no one has roleplay enabled</color>");
				} else {
					string msg = "";
					for (int c = 0; c < players.Count; c++) {
						msg += players[c];
						if (c < players.Count - 1) {
							msg += ", ";
						}
					}
					Chat.Send(causedBy, $"<color=yellow>Roleplay enabled:</color> {msg}");
				}
			}

			return true;
		}

	}
}

