using Chatting;
using Chatting.Commands;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

	public class SetJailCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/setjail")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "setjailposition")) {
				return true;
			}

			if (chattext.Equals("/setjail visitor")) {
				JailManager.setJailVisitorPosition(causedBy.Position);
				Chat.Send(causedBy, "Jail visiting position set");
				return true;
			}

			int x, y, z;
			if (splits.Count == 4) {
				if (!int.TryParse(splits[1], out x) || !int.TryParse(splits[2], out y) || !int.TryParse(splits[3], out z)) {
					Chat.Send(causedBy, "Syntax: /setjail {x y z | range}");
					return true;
				}
			} else if (splits.Count == 2) {
				int range;
				if (!int.TryParse(splits[1], out range)) {
					Chat.Send(causedBy, "Syntax: /setjail {x y z | range}");
					return true;
				}
				x = y = z = range;
			} else {
				x = y = z = JailManager.DEFAULT_RANGE;
			}

			JailManager.setJailPosition(causedBy, x, y, z);
			Chat.Send(causedBy, $"Jail set to your current position with bounds {x} {y} {z}");

			return true;
		}
	}
}

