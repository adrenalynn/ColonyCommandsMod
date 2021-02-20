using System.Text.RegularExpressions;
using System.Collections.Generic;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{

	public class BanChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/ban")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "ban")) {
				return true;
			}
			var m = Regex.Match(chattext, @"/ban (?<targetplayername>['].+[']|[^ ]+)");
			if (!m.Success) {
				Chat.Send(causedBy, "Syntax: /ban {targetplayername}");
				return true;
			}
			var targetPlayerName = m.Groups["targetplayername"].Value;
			Players.Player targetPlayer;
			string error;
			if (!PlayerHelper.TryGetPlayer(targetPlayerName, out targetPlayer, out error)) {
				Chat.Send(causedBy, $"Could not find target player '{targetPlayerName}'; {error}");
				return true;
			}
			Chat.Send(targetPlayer, "<color=red>You were banned from the server</color>");
			Chat.SendToConnected($"{targetPlayer.Name} is banned by {causedBy.Name}");
			BlackAndWhitelisting.AddBlackList(targetPlayer.ID.steamID.m_SteamID);
			BlackAndWhitelisting.Reload();
			Players.Disconnect(targetPlayer);

			foreach (Colony colony in targetPlayer.Colonies) {
				Log.Write($"Purging colony {colony.Name} from banned player {targetPlayer.Name}");
				if (colony.Owners.Length == 1) {
					ServerManager.ClientCommands.DeleteColonyAndBanner(null, colony, colony.Banners[0].Position);
				} else {
					colony.RemoveOwner(targetPlayer);
				}
			}
			return true;
		}
	}
}
