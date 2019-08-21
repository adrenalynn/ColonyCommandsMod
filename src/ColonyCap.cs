using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{
	public class ColonyCap : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (!splits[0].Equals("/colonycap")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "colonycap")) {
				return true;
			}

			if (splits.Count < 2) {
				Chat.Send(causedBy, "Syntax: /colonycap {colonistslimit} [checkintervalseconds]");
				if (AntiGrief.ColonistLimit > 0) {
					Chat.Send(causedBy, $"Current colonist limit is {AntiGrief.ColonistLimit}");
				} else {
					Chat.Send(causedBy, $"Number of colonists is currently unlimited");
				}
				return true;
			}

			int limit;
			if (!int.TryParse(splits[1], out limit)) {
				Chat.Send (causedBy, "Could not parse limit");
				return true;
			}

			AntiGrief.ColonistLimit = limit;
			if (AntiGrief.ColonistLimit > 0) {
				Chat.SendToConnected($"Colony population limit set to {AntiGrief.ColonistLimit}");
			} else {
				Chat.SendToConnected("Colony population limit disabled");
			}

			int interval;
			if (splits.Count > 2) {
				if (!int.TryParse(splits[2], out interval)) {
					Chat.Send(causedBy, "Could not parse interval");
					return true;
				}
				AntiGrief.ColonistLimitCheckSeconds = System.Math.Max(1, interval);
				Chat.Send(causedBy, $"Check interval seconds set to {AntiGrief.ColonistLimitCheckSeconds}");
			}

			AntiGrief.Save();
			AntiGrief.CheckColonistLimit();
			return true;
		}
	}
}

