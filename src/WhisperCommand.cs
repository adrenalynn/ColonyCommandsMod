﻿using System.Text.RegularExpressions;
using System.Collections.Generic;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{

	public class WhisperChatCommand : IChatCommand
	{

		public bool TryDoCommand (Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || (!splits[0].Equals("/w") && !splits[0].Equals("/whisper"))) {
				return false;
			}
			var m = Regex.Match(chattext, @"/((w)|(whisper)) (?<targetplayername>['].+[']|[^ ]+) (?<message>.+)");
			if (!m.Success) {
				Chat.Send(causedBy, "Syntax: /w [player] [message] or /whisper [player] [message]");
				return true;
			}
			var targetPlayerName = m.Groups["targetplayername"].Value;
			Players.Player targetPlayer;
			string error;
			if (!PlayerHelper.TryGetPlayer(targetPlayerName, out targetPlayer, out error)) {
				Chat.Send(causedBy, $"Could not find target player '{targetPlayerName}'; {error}");
				return true;
			}
			var message = m.Groups["message"].Value;
			Chat.Send(targetPlayer, $"<color=#00ffff>From [{causedBy.Name}]: {message}</color>");
			Chat.Send(causedBy, $"<color=#00ffff>To [{targetPlayer.Name}]: {message}</color>");
			return true;
		}
	}
}
