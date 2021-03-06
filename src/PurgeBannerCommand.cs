﻿using System.Collections.Generic;
using Pipliz;
using Chatting;
using Chatting.Commands;
using BlockEntities.Implementations;

namespace ColonyCommands
{
	public class PurgeBannerCommand : IChatCommand
	{
		public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
		{
			if (splits.Count == 0 || !splits[0].Equals("/purgebanner")) {
				return false;
			}
			if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "purgebanner")) {
				return true;
			}

			if (splits.Count == 3) {
				if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "purgeallbanner")) {
					return true;
				}
				// command: /purgebanner all <range> (Purge ALL colonies within range)
				if (splits[1].Equals("all")) {
					int range = 0;
					if (!int.TryParse(splits[2], out range)) {
						Chat.Send(causedBy, "Syntax: /purgebanner all <range>");
						return true;
					}
					int counter = PurgeAllColonies(causedBy, range);
					Chat.Send(causedBy, $"Purged {counter} colonies within range");
					return true;

				// command: /purgebanner days <minage> (Purge ALL colonies from inactive players)
				} else if (splits[1].Equals("days")) {
					int minage = int.MaxValue;
					if (!int.TryParse(splits[2], out minage)) {
						Chat.Send(causedBy, "Syntax: /purgebanner days <minage>");
						return true;
					}

					Dictionary<Players.Player, int> inactivePlayers = ActivityTracker.GetInactivePlayers(minage);
					int colonistCount = 0, counter = 0;
					foreach (Colony col in ServerManager.ColonyTracker.ColoniesByID.Values) {
						if (col.Owners.Length == 0) {
							continue;
						}
						bool activeOwnerFound = false;
						foreach (Players.Player owner in col.Owners) {
							if (!inactivePlayers.ContainsKey(owner)) {
								activeOwnerFound = true;
							}
						}
						if (!activeOwnerFound) {
							colonistCount += col.Followers.Count;
							counter++;
							PurgeColony(causedBy, col);
						}
					}
					Chat.Send(causedBy, $"Purged {counter} colonies with {colonistCount} colonists");
					return true;
				} else {
					Chat.Send(causedBy, "Syntax: /purgebanner {all|days} <range|age>");
					return true;
				}
			}

			// command: /purgebanner colony
			if (splits.Count == 2 && splits[1].Equals("colony")) {
				Colony colony = null;
				BannerTracker.Banner banner = null;
				int shortestDistance = int.MaxValue;
				foreach (Colony checkColony in ServerManager.ColonyTracker.ColoniesByID.Values) {
					foreach (BannerTracker.Banner checkBanner in checkColony.Banners) {
						int distX = (int)(causedBy.Position.x - checkBanner.Position.x);
						int distZ = (int)(causedBy.Position.z - checkBanner.Position.z);
						int distance = (int)System.Math.Sqrt(System.Math.Pow(distX, 2) + System.Math.Pow(distZ, 2));
						if (distance < shortestDistance) {
							shortestDistance = distance;
							banner = checkBanner;
							colony = checkColony;
						}
					}
				}

				if (banner == null) {
					Chat.Send(causedBy, "No banners found at all");
					return true;
				}
				if (shortestDistance > 100) {
					Chat.Send(causedBy, "Closest banner is more than 100 blocks away. Not doing anything");
					return true;
				}
				if (colony != null) {
					PurgeColony(causedBy, colony);
					Chat.Send(causedBy, $"Purged colony {colony.Name}");
					return true;
				}
			}

			// command: /purgebanner {playername}
			if (splits.Count == 2) {
				string targetName = splits[1];
				Players.Player target;
				string error;
				if (!PlayerHelper.TryGetPlayer(targetName, out target, out error, true)) {
					Chat.Send(causedBy, $"Could not find {targetName}: {error}");
					return true;
				}
				PurgePlayerColonies(causedBy, target);
				Chat.Send(causedBy, $"Freed {targetName} from all colonies");
				return true;
			}

			// only reached if nothing else matches
			Chat.Send(causedBy, "Syntax: /purgebanner { colony | [playername] | all [range] | days [age] }");
			return true;
		}

		// purge a full colony at once
		public void PurgeColony(Players.Player causedBy, Colony colony)
		{
			Log.Write($"Purged colony {colony.Name} id={colony.ColonyID}");
			while (colony.Banners.Length > 1) {
				ServerManager.ClientCommands.DeleteBannerTo(causedBy, colony, colony.Banners[0].Position);
			}
			ServerManager.ClientCommands.DeleteColonyAndBanner(causedBy, colony, colony.Banners[0].Position);
			return;
		}

		// purge all colonies of a given player (or remove him/her in case of multiple owners)
		public int PurgePlayerColonies(Players.Player causedBy, Players.Player target)
		{
			int i = 0;
			foreach (Colony colony in target.Colonies) {
				if (colony.Banners.Length == 0) {
					Log.Write($"Colony {colony.Name} does not have any banners!");
					continue;
				}
				Log.Write($"Purging colony {colony.Name} from player {target.Name}");
				if (colony.Owners.Length == 1) {
					ServerManager.ClientCommands.DeleteColonyAndBanner(causedBy, colony, colony.Banners[0].Position);
				} else {
					colony.RemoveOwner(target);
				}
				i++;
			}
			return i;
		}

		// purge all colonies within a given range
		public int PurgeAllColonies(Players.Player causedBy, int range)
		{
			List<Colony> colonies = new List<Colony>();
			foreach (Colony checkColony in ServerManager.ColonyTracker.ColoniesByID.Values) {
				if (checkColony.Banners.Length == 0) {
					Log.Write($"colony id={checkColony.ColonyID} has no banners");
					continue;
				}
				BannerTracker.Banner closestBanner = checkColony.GetClosestBanner(causedBy.VoxelPosition);
				if (Pipliz.Math.ManhattanDistance(closestBanner.Position, causedBy.VoxelPosition) <= range) {
					colonies.Add(checkColony);
				}
			}

			// second loop for actual deletion
			int counter = 0;
			foreach (Colony colony in colonies) {
				while (colony.Banners.Length > 1) {
					ServerManager.ClientCommands.DeleteBannerTo(causedBy, colony, colony.Banners[0].Position);
				}
				Chat.Send(causedBy, $"Purging {colony.Name}");
				ServerManager.ClientCommands.DeleteColonyAndBanner(causedBy, colony, colony.Banners[0].Position);
				counter++;
			}

			return counter;
		}

	}
}

