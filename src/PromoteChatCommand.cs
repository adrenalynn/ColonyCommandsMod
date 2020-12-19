using Chatting;
using Chatting.Commands;
using System.Collections.Generic;

namespace ColonyCommands
{

	public class PromoteChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/promote")) {
				return false;
			}

			if (splits.Count != 3) {
				Chat.Send(causedBy, "Syntax: /promote {group} {user}");
				return true;
			}

			// permission group
			string groupName = splits[1];
			if (!PermissionsManager.Groups.ContainsKey(groupName)) {
				Chat.Send(causedBy, $"No group {groupName} found");
				return true;
			}

			// check permission of command user
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, "permissions.setgroup." + groupName.ToLower())) {
				return true;
			}

			// target player
			string targetName = splits[2];
			Players.Player target;
			string error;
			if (!PlayerHelper.TryGetPlayer(targetName, out target, out error, true)) {
				Chat.Send(causedBy, $"Could not find player {targetName}: {error}");
				return true;
			}

			PermissionsManager.SetGroupOfUser(causedBy, target, groupName);

			return true;
		}

	}
}

