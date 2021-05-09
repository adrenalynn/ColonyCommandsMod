using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

	public class RoleplayBanChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/rpban")) {
				return false;
			}

			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "roleplayban")) {
				return true;
			}

			var m = Regex.Match(chattext, @"/rpban (?<player>[""'].+[""']|[^ ]+) (?<time>[0-9]+)(?<unit>[a-zA-Z]) (?<reason>.+)$");
			if (!m.Success) {
				Chat.Send(causedBy, "Syntax: /rpban {player} {duration}[m|h|d] {reason}");
				return true;
			}

			Players.Player target;
			string error;
			string targetName = m.Groups["player"].Value;
			if (!PlayerHelper.TryGetPlayer(targetName, out target, out error, true)) {
				Chat.Send(causedBy, $"Could not find player {targetName}: {error}");
				return true;
			}

			long duration = 0;
			string timeval = m.Groups["time"].Value;
			if (!long.TryParse(timeval, out duration)) {
				Chat.Send(causedBy, $"No valid time value: {timeval}");
				return true;
			}
			string timeunit = m.Groups["unit"].Value;
			switch (timeunit[0]) {
				case 'm':
				case 'M':
					duration *= 60;
					break;
				case 'h':
				case 'H':
					duration *= 60 * 60;
					break;
				case 'd':
				case 'D':
					duration *= 60 * 60 * 24;
					break;
				default:
					Chat.Send(causedBy, $"Invalid unit {timeunit}: please use one of m/h/d for minutes/hours/days");
					return true;
			}

			if (RoleplayManager.IsRoleplayBanned(target)) {
				Chat.Send(causedBy, $"{target.Name} is already banned");
				return true;
			}

			string reason = m.Groups["reason"].Value;
			string prettytime = RoleplayManager.prettyPrintDuration(duration);

			RoleplayManager.BanPlayer(causedBy, target, duration, reason);

			Chat.Send(causedBy, $"<color=yellow>Banned {target.Name} from roleplay for {prettytime}: {reason}</color>");
			if (target.ConnectionState == Players.EConnectionState.Connected) {
				Chat.Send(target, $"<color=yellow>{causedBy.Name} banned you from roleplay for {prettytime}: {reason}</color>");
			}
			return true;
		}

	}


	public class RoleplayUnbanChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/rpunban")) {
				return false;
			}

			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "roleplayban")) {
				return true;
			}

			if (splits.Count != 2) {
				Chat.Send(causedBy, "Syntax: /rpunban {player}");
				return true;
			}

			Players.Player target;
			string error;
			if (!PlayerHelper.TryGetPlayer(splits[1], out target, out error, true)) {
				Chat.Send(causedBy, $"Could not find player {splits[1]}: {error}");
				return true;
			}

			if (RoleplayManager.UnbanPlayer(target)) {
				Chat.Send(causedBy, $"Unbanned {target.Name} from roleplaying");
				if (target.ConnectionState == Players.EConnectionState.Connected) {
					Chat.Send(target, $"{causedBy.Name} unbanned you from roleplaying. You can /rp on again");
				}
			} else {
				Chat.Send(causedBy, "{target.Name} is not banned from roleplaying");
			}
			return true;
		}
	}

}

