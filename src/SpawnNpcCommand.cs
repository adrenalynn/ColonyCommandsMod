using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NPC;
using BlockEntities.Implementations;

namespace ColonyCommands
{

	public class SpawnNpcCommand : IChatCommand
	{

		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (!splits[0].Equals("/spawnnpc")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "npcandbeds")) {
				return true;
			}

			Match m = Regex.Match(chattext, @"/spawnnpc (?<amount>\d+) ?(?<player>['].+[']|[^ ]+)?");
			if (!m.Success) {
				Chat.Send(causedBy, "Syntax: /spawnnpc {number} [targetplayer]");
				return true;
			}
			int amount = 0;
			if (!int.TryParse(m.Groups["amount"].Value, out amount) || amount <= 0) {
				Chat.Send(causedBy, "Number should be > 0");
				return true;
			}

			Players.Player target = causedBy;
			string error;
			if (!m.Groups["player"].Value.Equals("")) {
				if (!PlayerHelper.TryGetPlayer(m.Groups["player"].Value, out target, out error)) {
					Chat.Send(causedBy, "Could not find target: {error}");
				}
			}

			Colony colony = target.ActiveColony;
			if (colony == null) {
				Chat.Send(target, "You need to be at an active colony to spawn beds");
				if (causedBy != target) {
					Chat.Send(causedBy, " {target} has no active colony");
				}
				return true;
			}
			BannerTracker.Banner banner = colony.GetClosestBanner(causedBy.VoxelPosition);
			if (banner == null) {
				Chat.Send(causedBy, "No banners found for the active colony");
				return true;
			}

			for (int i = 0; i < amount; i++) {
				NPCBase npc = new NPCBase(colony, banner.Position);
				NPCTracker.Add(npc);
				colony.RegisterNPC(npc);
				ModLoader.Callbacks.OnNPCRecruited.Invoke(npc);
			}

			Chat.Send(causedBy, $"Spawned {amount} colonists");
			return true;
		}
	}
}

