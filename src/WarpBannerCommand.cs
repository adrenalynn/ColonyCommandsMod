using System.Text.RegularExpressions;
using System.Collections.Generic;
using Pipliz;
using Chatting;
using Chatting.Commands;
using BlockEntities.Implementations;

namespace ColonyCommands
{

	public class WarpBannerChatCommand : IChatCommand
	{

		public bool TryDoCommand (Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/warpbanner")) {
				return false;
			}

			Colony targetColony = null;
			Match m = Regex.Match(chattext, @"/warpbanner (?<target>.+)");
			if (m.Success) {
				string error;
				if (!PlayerHelper.TryGetColony(m.Groups["target"].Value, out targetColony, out error)) {
					Chat.Send(causedBy, $"Could not find target: {error}");
					return true;
				}
			} else {
				int minDistance = int.MaxValue;
				Colony[] colonies = causedBy.Colonies;
				for (int i = 0; i < colonies.Length; i++) {
					BannerTracker.Banner found;
					int closestDistance = colonies[i].Banners.GetClosestDistance(causedBy.VoxelPosition, out found);
					if (closestDistance < minDistance) {
						targetColony = colonies[i];
						minDistance = closestDistance;
					}
				}
				if (targetColony == null) {
					Chat.Send(causedBy, $"Could not find any banner to warp to");
					return true;
				}
			}

			string permission = AntiGrief.MOD_PREFIX + "warp.banner";
			if (targetColony != null) {
				if (targetColony.Owners.ContainsByReference(causedBy)) {
					permission += ".self";
				}
				if (!PermissionsManager.CheckAndWarnPermission(causedBy, permission)) {
					return true;
				}
				Helper.TeleportPlayer(causedBy, targetColony.Banners[0].Position.Vector);
			}

			return true;
		}
	}
}

