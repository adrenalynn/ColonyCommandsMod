using Chatting;
using Chatting.Commands;
using System.Collections.Generic;
using System;

namespace ColonyCommands
{

	public class GracePeriodChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/graceperiod")) {
				return false;
			}

			// change setting
			if (splits.Count == 2) {
				if (!PermissionsManager.CheckAndWarnPermission(causedBy, "antigrief.setgraceperiod")) {
					return true;
				}
				int minutes = 0;
				if (!int.TryParse(splits[1], out minutes)) {
					Chat.Send(causedBy, "Syntax: /graceperiod {minutes}");
					return true;
				}

				AntiGrief.StartupGracePeriod = minutes * 60;
			}

			string uptimeval;
			int upseconds = Pipliz.Time.SecondsSinceStartInt;
			if (upseconds > 3600) {
				uptimeval = $"{upseconds/3600}:{upseconds%3600/60:D2} hours.";
			} else if (upseconds > 60) {
				uptimeval = $"{upseconds/60}:{upseconds%60:D2} minutes.";
			} else {
				uptimeval = $"{upseconds} seconds.";
			}

			Chat.Send(causedBy, $"Startup grace period is {AntiGrief.StartupGracePeriod / 60} minutes. Server uptime is {uptimeval}");
			return true;
		}

	}
}

