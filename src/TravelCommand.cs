using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{
	public class TravelChatCommand : IChatCommand
	{
		private Vector3Int cachedStartpoint = Vector3Int.invalidPos;

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (!splits[0].Equals("/travel")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "travelpaths")) {
				return true;
			}

			// overwrite start point
			if (splits.Count == 2 && splits[1].Equals("start")) {
				cachedStartpoint = causedBy.VoxelPosition;
				Chat.Send(causedBy, "Aborted path and resetted start point to your current position");
				return true;
			}

			// remove path
			if (splits.Count == 2 && splits[1].Equals("remove")) {
				if (TravelManager.removePath(causedBy, causedBy.VoxelPosition)) {
					Chat.Send(causedBy, "Removed travel path at this position");
				} else {
					Chat.Send(causedBy, "Could not find a travel point nearby");
				}
				return true;
			}

			// set startpoint if none exists
			if (cachedStartpoint == Vector3Int.invalidPos) {
				cachedStartpoint = causedBy.VoxelPosition;
				Chat.Send(causedBy, "Starting a new travel path. Type /travel again to set its endpoint");
			} else {
				if (TravelManager.addPath(causedBy, cachedStartpoint, causedBy.VoxelPosition)) {
					Chat.Send(causedBy, $"Created a new travel path between {cachedStartpoint} and {causedBy.VoxelPosition}");
					cachedStartpoint = Vector3Int.invalidPos;
				} else {
					Chat.Send(causedBy, "Could not create travel path. Too close to an existing one?");
				}
			}

			return true;
		}

	} // class

} //namespace

