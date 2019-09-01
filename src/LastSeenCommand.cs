using System.Collections.Generic;
using System.Text.RegularExpressions;
using Chatting;
using Chatting.Commands;

namespace ColonyCommands
{

	public class LastSeenChatCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (!splits[0].Equals("/lastseen")) {
				return false;
			}
			var m = Regex.Match(chattext, @"/lastseen (?<playername>['].+[']|[^ ]+)");
			if (!m.Success) {
				Chat.Send(causedBy, "Syntax: /lastseen [playername]");
				return true;
			}

			Players.Player targetPlayer;
			string targetPlayerName = m.Groups["playername"].Value;
			string error;
			if (!PlayerHelper.TryGetPlayer(targetPlayerName, out targetPlayer, out error, true)) {
				Chat.Send(causedBy, $"Could not find player '{targetPlayerName}'; {error}");
				return true;
			}

			ActivityTracker.StatsDataEntry stats = ActivityTracker.GetOrCreateStats(targetPlayer.ID.ToStringReadable());
			string timePlayed = $"{System.Math.Truncate(stats.secondsPlayed / 3600f)}:{System.Math.Truncate((stats.secondsPlayed % 3600f) / 60f):00}:{stats.secondsPlayed % 60f:00}";
			Chat.Send(causedBy, $"Player {targetPlayer.ID.ToStringReadable()} last seen {stats.lastSeen}. Playtime: {timePlayed}");
			return true;
		}
	}
}
