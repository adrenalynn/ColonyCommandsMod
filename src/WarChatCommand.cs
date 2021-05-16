using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

	public class WarChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/war")) {
				return false;
			}

			// start war mode
			if (splits.Count >= 2 && splits[1].Equals("start")) {
				int duration = 0;
				if (splits.Count > 2) {
					duration = TryGetDurationSeconds(splits[2]);
					if (duration < 0) {
						Chat.Send(causedBy, "Syntax: /war start <4h>  (use values like 90m, 2h, 4h)");
						return true;
					}
				}
				if (duration < AntiGrief.WarDuration) {
					duration = AntiGrief.WarDuration;
				}

				// require active colony with colonists. To always have war two sided
				if (causedBy.ActiveColony == null || causedBy.ActiveColony.Followers.Count < 10) {
					Chat.Send(causedBy, "<color=red>You need to be at an active colony with colonists to start wars</color>");
					return true;
				}

				// require roleplay marked
				if (!RoleplayManager.IsRoleplaying(causedBy)) {
					Chat.Send(causedBy, "<color=red>War is only allowed as roleplay. Use /rp on first</color>");
					return true;
				}

				WarManager.EnableWar(causedBy, duration);
				Chat.SendToConnectedBut(causedBy, $"<color=yellow>{causedBy.Name} entered WAR mode</color>");
				Chat.Send(causedBy, $"<color=yellow>You entered WAR mode. It will expire after {duration / 60 / 60} hours.</color>");

			// admin disable all war
			} else if (splits.Count == 2 && splits[1].Equals("end")) {
				if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "endwar")) {
					return true;
				}
				WarManager.EndAllWars();
				Chat.SendToConnected($"<color=yellow>{causedBy.Name} did end all wars.</color>");

			// or list war mode players
			} else {
				List<string> players = WarManager.PlayerList;
				if (players.Count == 0) {
					Chat.Send(causedBy, "<color=yellow>No war ongoing currently.</color>");
				} else {
					string msg = "";
					for (int c = 0; c < players.Count; c++) {
						msg += players[c];
						if (c < players.Count - 1) {
							msg += ", ";
						}
					}
					Chat.Send(causedBy, $"<color=yellow>WAR enabled:</color> {msg}");
				}
			}

			return true;
		}

		// try to read values like 2h, 5m and so on
		private int TryGetDurationSeconds(string input)
		{

			var m = Regex.Match(input, @"(?<value>[0-9]+)?(?<unit>[mMhH])");
			int val;
			if (!m.Success || !int.TryParse(m.Groups["value"].Value, out val)) {
				return -1;
			}
			string unit = m.Groups["unit"].Value;

			if (unit.Length > 0) {
				switch (unit[0]) {
					case 'm':
					case 'M': val *= 60; break;
					case 'h':
					case 'H': val *= 60 * 60; break;
				}
			}
			return val;
		}

	}
}

