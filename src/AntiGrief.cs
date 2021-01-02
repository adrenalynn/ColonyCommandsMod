using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using Pipliz;
using Chatting;
using Chatting.Commands;
using Pipliz.JSON;
using TerrainGeneration;
using BlockEntities.Implementations;
using NPC;
using Jobs;
using UnityEngine;
using Shared.Networking;
using MeshedObjects;

namespace ColonyCommands {

	[ModLoader.ModManager]
	public static class AntiGrief
	{
		public const string MOD_PREFIX = "mods.scarabol.commands.";
		public const string NAMESPACE = "AntiGrief";
		public static string MOD_DIRECTORY;
		public const string PERMISSION_SUPER = "mods.scarabol.antigrief";
		public const string PERMISSION_SPAWN_CHANGE = PERMISSION_SUPER + ".spawnchange";
		public const string PERMISSION_BANNER_PREFIX = PERMISSION_SUPER + ".banner.";
		private const string COLONY_ID_FORMAT = "colony.{0:0000000000}";
		static int SpawnProtectionRangeXPos;
		static int SpawnProtectionRangeXNeg;
		static int SpawnProtectionRangeZPos;
		static int SpawnProtectionRangeZNeg;
		static int BannerProtectionRangeX;
		static int BannerProtectionRangeZ;
		public static int ColonistLimit;
		public static int ColonistLimitCheckSeconds;
		public static int ColonistLimitMaxKillPerIteration;
		static List<int> ColonistTierLimits = new List<int>();
		static List<List<int>> ColonistPerColonyTierLimits = new List<List<int>>();
		private static float ColonyColonistLimitTierCheckSeconds;
		private static int ColonyColonistLimitTierWarnTimes;
		private static Dictionary<Colony, int> ColonyColonistLimitTiers = new Dictionary<Colony, int>();
		public static int OnlineBackupIntervalHours;
		public static List<CustomProtectionArea> CustomAreas = new List<CustomProtectionArea>();
		static int NpcKillsJailThreshold;
		static int NpcKillsKickThreshold;
		static int NpcKillsBanThreshold;
		static bool EnableWarpCommand;
		public static int WarDuration = 2 * 60 * 60; // 2 hours
		static Dictionary<Players.Player, int> KillCounter = new Dictionary<Players.Player, int>();
		public static MethodInfo AngryGuardsWarMode = null;
		public static int StartupGracePeriod = 0;
		public static long ServerStartupTime;

		static string ConfigFilepath {
			get {
				return Path.Combine(Path.Combine("gamedata", "savegames"), Path.Combine(ServerManager.WorldName, "antigrief-config.json"));
			}
		}

		[ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, NAMESPACE + ".OnAssemblyLoaded")]
		public static void OnAssemblyLoaded(string path)
		{
			MOD_DIRECTORY = Path.GetDirectoryName(path);
			Log.Write("Loaded ColonyCommands (Anti-Grief)");
			ServerStartupTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
		}

		[ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, NAMESPACE + ".RegisterTypes")]
		public static void AfterItemTypesDefined()
		{
			Log.Write("Registering commands (Anti-Grief)");
			CommandManager.RegisterCommand(new AnnouncementsChatCommand());
			CommandManager.RegisterCommand(new AntiGriefChatCommand());
			CommandManager.RegisterCommand(new CustomAreaCommand());
			CommandManager.RegisterCommand(new BanChatCommand());
			CommandManager.RegisterCommand(new BannerNameChatCommand());
			CommandManager.RegisterCommand(new BetterChatCommand());
			CommandManager.RegisterCommand(new ColonyCap());
			CommandManager.RegisterCommand(new DrainChatCommand());
			CommandManager.RegisterCommand(new GodChatCommand());
			CommandManager.RegisterCommand(new InactiveChatCommand());
			CommandManager.RegisterCommand(new ItemIdChatCommand());
			CommandManager.RegisterCommand(new KickChatCommand());
			CommandManager.RegisterCommand(new KillNPCsChatCommand());
			CommandManager.RegisterCommand(new KillPlayerChatCommand());
			CommandManager.RegisterCommand(new LastSeenChatCommand());
			CommandManager.RegisterCommand(new NoFlightChatCommand());
			CommandManager.RegisterCommand(new OnlineChatCommand());
			CommandManager.RegisterCommand(new ServerPopCommand());
			CommandManager.RegisterCommand(new StuckChatCommand());
			CommandManager.RegisterCommand(new TopChatCommand());
			CommandManager.RegisterCommand(new TradeChatCommand());
			CommandManager.RegisterCommand(new TrashChatCommand());
			CommandManager.RegisterCommand(new TravelChatCommand());
			CommandManager.RegisterCommand(new WarpBannerChatCommand());
			CommandManager.RegisterCommand(new WarpPlaceChatCommand());
			CommandManager.RegisterCommand(new WarpSpawnChatCommand());
			CommandManager.RegisterCommand(new WhisperChatCommand());
			CommandManager.RegisterCommand(new SetJailCommand());
			CommandManager.RegisterCommand(new JailCommand());
			CommandManager.RegisterCommand(new JailReleaseCommand());
			CommandManager.RegisterCommand(new JailVisitCommand());
			CommandManager.RegisterCommand(new JailLeaveCommand());
			CommandManager.RegisterCommand(new JailRecCommand());
			CommandManager.RegisterCommand(new JailTimeCommand());
			CommandManager.RegisterCommand(new AreaShowCommand());
			CommandManager.RegisterCommand(new HelpCommand());
			CommandManager.RegisterCommand(new ColorTestCommand());
			CommandManager.RegisterCommand(new SpawnNpcCommand());
			CommandManager.RegisterCommand(new BedsCommand());
			CommandManager.RegisterCommand(new PurgeBannerCommand());
			CommandManager.RegisterCommand(new MuteChatCommand());
			CommandManager.RegisterCommand(new UnmuteChatCommand());
			CommandManager.RegisterCommand(new ListPlayerChatCommand());
			CommandManager.RegisterCommand(new WarChatCommand());
			CommandManager.RegisterCommand(new PromoteChatCommand());
			CommandManager.RegisterCommand(new GracePeriodChatCommand());
			return;
		}

		[ModLoader.ModCallback (ModLoader.EModCallbackType.OnTryChangeBlock, NAMESPACE + ".OnTryChangeBlock")]
		public static void OnTryChangeBlock (ModLoader.OnTryChangeBlockData userData)
		{
			// TryChangeBlock can be caused by both players and colonies (Builder/Digger)
			Players.Player causedBy = null;
			if (userData.RequestOrigin.Type == BlockChangeRequestOrigin.EType.Player) {
				causedBy = userData.RequestOrigin.AsPlayer;
			} else if (userData.RequestOrigin.Type == BlockChangeRequestOrigin.EType.Colony) {
				Colony colony = userData.RequestOrigin.AsColony;
				if (colony == null) {
					return;
				}
				causedBy = colony.Owners[0];	// colony leader
			}
			if (causedBy == null) {
				return;
			}

			Pipliz.Vector3Int playerPos = userData.Position;

			// allow staff members
			if (PermissionsManager.HasPermission(causedBy, PERMISSION_SUPER)) {
				return;
			}

			if (userData.TypeNew == BlockTypes.BuiltinBlocks.Types.water && !PermissionsManager.HasPermission(causedBy, MOD_PREFIX + ".placewater")) {
				Chat.Send(causedBy, "<color=red>You don't have permission to place this block!</color>");
				BlockCallback(userData);
				return;
			}

			// check spawn area
			int ox = playerPos.x - ServerManager.GetSpawnPoint().Position.x;
			int oz = playerPos.z - ServerManager.GetSpawnPoint().Position.z;
			if (((ox >= 0 && ox <= SpawnProtectionRangeXPos) || (ox < 0 && ox >= -SpawnProtectionRangeXNeg)) && ((oz >= 0 && oz <= SpawnProtectionRangeZPos) || (oz < 0 && oz >= -SpawnProtectionRangeZNeg))) {
				if (!PermissionsManager.HasPermission(causedBy, PERMISSION_SPAWN_CHANGE)) {
					if (causedBy.ConnectionState == Players.EConnectionState.Connected) {
						Chat.Send(causedBy, "<color=red>You don't have permission to change the spawn area!</color>");
					}
					BlockCallback(userData);
					return;
				}
			}

			// Check all banners and then decide by Colony.Owners if allowed or not
			int checkRangeX = BannerProtectionRangeX;
			int checkRangeZ = BannerProtectionRangeZ;
			if (userData.TypeNew.ItemIndex == BlockTypes.BuiltinBlocks.Indices.banner) {
				checkRangeX *= 2;
				checkRangeZ *= 2;
			}
			foreach (Colony checkColony in ServerManager.ColonyTracker.ColoniesByID.Values) {

				foreach (BannerTracker.Banner checkBanner in checkColony.Banners) {
					int distanceX = (int)System.Math.Abs(playerPos.x - checkBanner.Position.x);
					int distanceZ = (int)System.Math.Abs(playerPos.z - checkBanner.Position.z);

					if (distanceX < checkRangeX && distanceZ < checkRangeZ) {
						foreach (Players.Player owner in checkColony.Owners) {
							if (owner == causedBy) {
								return;
							}
						}
						// check if /antigrief permission - only done for banner placement
						// after the banner is placed the player will be an owner of the colony
						if (userData.TypeNew.ItemIndex == BlockTypes.BuiltinBlocks.Indices.banner) {

							// permission for this colony id
							if (PermissionsManager.HasPermission(causedBy, PERMISSION_BANNER_PREFIX + string.Format(COLONY_ID_FORMAT, checkColony.ColonyID))) {
								return;
							}

							// permission for all colonies of the owner
							foreach (Players.Player owner in checkColony.Owners) {
								if (PermissionsManager.HasPermission(causedBy, PERMISSION_BANNER_PREFIX + owner.ID.steamID)) {
									return;
								}
							}
						}

						if (userData.TypeNew.ItemIndex == BlockTypes.BuiltinBlocks.Indices.banner) {
							int tooCloseX = checkRangeX - distanceX;
							int tooCloseZ = checkRangeZ - distanceZ;
							int moveBlocks = (tooCloseX > tooCloseZ) ? tooCloseX : tooCloseZ;
							if (causedBy.ConnectionState == Players.EConnectionState.Connected) {
								Chat.Send(causedBy, $"<color=red>Too close to another banner! Please move {moveBlocks} blocks further</color>");
							}
						} else {
							if (causedBy.ConnectionState == Players.EConnectionState.Connected) {
								Chat.Send(causedBy, "<color=red>No permission to change blocks near this banner!</color>");
							}
						}
						BlockCallback(userData);
						return;
					}
				}
			}

			// check custom protection areas
			foreach (CustomProtectionArea area in CustomAreas) {
				if (area.Contains(playerPos) && !PermissionsManager.HasPermission(causedBy, PERMISSION_SPAWN_CHANGE)) {
					if (causedBy.ConnectionState == Players.EConnectionState.Connected) {
						Chat.Send(causedBy, "<color=red>You don't have permission to change this protected area!</color>");
					}
					BlockCallback(userData);
					return;
				}
			}

			return;
		}

		// Block (deny) a TryChangeBlock event
		static void BlockCallback(ModLoader.OnTryChangeBlockData userData)
		{
			userData.CallbackState = ModLoader.OnTryChangeBlockData.ECallbackState.Cancelled;
			userData.InventoryItemResults.Clear();
		}

		// load everything after the world starts
		[ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, NAMESPACE + ".AfterWorldLoaded")]
		public static void AfterWorldLoad()
		{
			Load();
			JailManager.Load();
			TravelManager.Load();
			CheckColonistLimit();
			WarManager.CheckWarStatus();
			ChatColors.LoadChatColors();

			if (OnlineBackupIntervalHours > 0) {
				Log.Write($"Found online backup interval setting {OnlineBackupIntervalHours}h");
				ThreadManager.InvokeOnMainThread(delegate {
					PerformOnlineBackup();
				}, OnlineBackupIntervalHours * 60f * 60f);
			}

			if (ColonistPerColonyTierLimits.Count > 0) {
				Log.Write("Found per colony tier/difficulty limit settings");
				ThreadManager.InvokeOnMainThread(delegate {
					PerformColonistPerColonyLimitCheck();
				}, ColonyColonistLimitTierCheckSeconds);
			}
		}

		// load config
		public static void Load()
		{
			SpawnProtectionRangeXPos = 50;
			SpawnProtectionRangeXNeg = 50;
			SpawnProtectionRangeZPos = 50;
			SpawnProtectionRangeZNeg = 50;
			BannerProtectionRangeX = 50;
			BannerProtectionRangeZ = 50;
			CustomAreas.Clear();
			JSONNode jsonConfig;
			if (JSON.Deserialize (ConfigFilepath, out jsonConfig, false)) {
				int rx;
				if (jsonConfig.TryGetAs ("SpawnProtectionRangeX+", out rx)) {
					SpawnProtectionRangeXPos = rx;
				} else if (jsonConfig.TryGetAs ("SpawnProtectionRangeX", out rx)) {
					SpawnProtectionRangeXPos = rx;
				} else {
					Log.Write ($"Could not get SpawnProtectionRangeX+ or SpawnProtectionRangeX from json config, using default value {SpawnProtectionRangeXPos}");
				}
				if (jsonConfig.TryGetAs ("SpawnProtectionRangeX-", out rx)) {
					SpawnProtectionRangeXNeg = rx;
				} else if (jsonConfig.TryGetAs ("SpawnProtectionRangeX", out rx)) {
					SpawnProtectionRangeXNeg = rx;
				} else {
					Log.Write ($"Could not get SpawnProtectionRangeX- or SpawnProtectionRangeX from json config, using default value {SpawnProtectionRangeXNeg}");
				}
				int rz;
				if (jsonConfig.TryGetAs ("SpawnProtectionRangeZ+", out rz)) {
					SpawnProtectionRangeZPos = rz;
				} else if (jsonConfig.TryGetAs ("SpawnProtectionRangeZ", out rz)) {
					SpawnProtectionRangeZPos = rz;
				} else {
					Log.Write ($"Could not get SpawnProtectionRangeZ+ or SpawnProtectionRangeZ from json config, using default value {SpawnProtectionRangeZPos}");
				}
				if (jsonConfig.TryGetAs ("SpawnProtectionRangeZ-", out rz)) {
					SpawnProtectionRangeZNeg = rz;
				} else if (jsonConfig.TryGetAs ("SpawnProtectionRangeZ", out rz)) {
					SpawnProtectionRangeZNeg = rz;
				} else {
					Log.Write ($"Could not get SpawnProtectionRangeZ- or SpawnProtectionRangeZ from json config, using default value {SpawnProtectionRangeZNeg}");
				}
				if (!jsonConfig.TryGetAs ("BannerProtectionRangeX", out BannerProtectionRangeX)) {
					Log.Write ($"Could not get banner protection x-range from json config, using default value {BannerProtectionRangeX}");
				}
				if (!jsonConfig.TryGetAs ("BannerProtectionRangeZ", out BannerProtectionRangeZ)) {
					Log.Write ($"Could not get banner protection z-range from json config, using default value {BannerProtectionRangeZ}");
				}
				JSONNode jsonCustomAreas;
				if (jsonConfig.TryGetAs ("CustomAreas", out jsonCustomAreas) && jsonCustomAreas.NodeType == NodeType.Array) {
					foreach (var jsonCustomArea in jsonCustomAreas.LoopArray ()) {
						try {
							CustomAreas.Add (new CustomProtectionArea (jsonCustomArea));
						} catch (Exception exception) {
							Log.WriteError ($"Exception loading custom area; {exception.Message}");
						}
					}
					Log.Write ($"Loaded {CustomAreas.Count} from file");
				}
				jsonConfig.TryGetAsOrDefault("NpcKillsJailThreshold", out NpcKillsJailThreshold, 2);
				jsonConfig.TryGetAsOrDefault("NpcKillsKickThreshold", out NpcKillsKickThreshold, 5);
				jsonConfig.TryGetAsOrDefault("NpcKillsBanThreshold", out NpcKillsBanThreshold, 6);
				jsonConfig.TryGetAsOrDefault("WarDuration", out WarDuration, 2 * 60 * 60);

				jsonConfig.TryGetAsOrDefault("ColonistLimit", out ColonistLimit, 0);
				jsonConfig.TryGetAsOrDefault("ColonistCheckInterval", out ColonistLimitCheckSeconds, 30);
				jsonConfig.TryGetAsOrDefault("ColonistLimitMaxKillPerIteration", out ColonistLimitMaxKillPerIteration, 500);
				jsonConfig.TryGetAsOrDefault("OnlineBackupIntervalHours", out OnlineBackupIntervalHours, 0);

				JSONNode jsonCapacityTiers;
				if (jsonConfig.TryGetAs("ColonistCapacityTiers", out jsonCapacityTiers) && jsonCapacityTiers.NodeType == NodeType.Array) {
					foreach (JSONNode jsonVal in jsonCapacityTiers.LoopArray()) {
						int val = jsonVal.GetAs<int>();
						ColonistTierLimits.Add(val);
						Log.Write($"Colonist limit tier{ColonistTierLimits.Count} is {val}");
					}
				}

				// colonists per colony tier limits
				JSONNode jsonTiers;
				if (jsonConfig.TryGetAs("ColonistLimitsColonyDifficultyTiers", out jsonTiers) && jsonTiers.NodeType == NodeType.Array) {
					foreach (JSONNode jsonDifficulties in jsonTiers.LoopArray()) {
						List<int> difficultyLimits = new List<int>();
						foreach (JSONNode jsonVal in jsonDifficulties.LoopArray()) {
							difficultyLimits.Add(jsonVal.GetAs<int>());
						}
						ColonistPerColonyTierLimits.Add(difficultyLimits);
						Log.Write($"Found {difficultyLimits.Count} colony difficulty colonist limits for tier{ColonistPerColonyTierLimits.Count}");
					}
					jsonConfig.TryGetAsOrDefault("ColonyColonistLimitTierCheckSeconds", out ColonyColonistLimitTierCheckSeconds, 63f);
					jsonConfig.TryGetAsOrDefault("ColonyColonistLimitTierWarnTimes", out ColonyColonistLimitTierWarnTimes, 3);
				}

				// check warp command option for compatibility with other mods
				jsonConfig.TryGetAsOrDefault("EnableWarpCommand", out EnableWarpCommand, true);
				if (EnableWarpCommand) {
					Log.Write("Enabling /warp command");
					CommandManager.RegisterCommand(new WarpChatCommand());
				}

				int warpRange;
				jsonConfig.TryGetAsOrDefault("DefaultWarpRange", out warpRange, 2);
				TravelManager.DefaultWarpRange = warpRange;

				jsonConfig.TryGetAsOrDefault("StartupGracePeriod", out StartupGracePeriod, 0);

			} else {
				Save();
				Log.Write ($"Could not find {ConfigFilepath} file, created default one");
			}

			Log.Write ($"Using spawn protection with X+ range {SpawnProtectionRangeXPos}");
			Log.Write ($"Using spawn protection with X- range {SpawnProtectionRangeXNeg}");
			Log.Write ($"Using spawn protection with Z+ range {SpawnProtectionRangeZPos}");
			Log.Write ($"Using spawn protection with Z- range {SpawnProtectionRangeZNeg}");
			Log.Write ($"Using banner protection with X range {BannerProtectionRangeX}");
			Log.Write ($"Using banner protection with Z range {BannerProtectionRangeZ}");
		}

		public static void AddCustomArea (CustomProtectionArea area)
		{
			CustomAreas.Add(area);
			Save();
		}

		public static void RemoveCustomArea(CustomProtectionArea area)
		{
			CustomAreas.Remove(area);
			Save();
		}

		// save config
		public static void Save()
		{
			JSONNode jsonConfig;
			if (!JSON.Deserialize (ConfigFilepath, out jsonConfig, false)) {
				jsonConfig = new JSONNode ();
			}
			jsonConfig.SetAs("SpawnProtectionRangeX+", SpawnProtectionRangeXPos);
			jsonConfig.SetAs("SpawnProtectionRangeX-", SpawnProtectionRangeXNeg);
			jsonConfig.SetAs("SpawnProtectionRangeZ+", SpawnProtectionRangeZPos);
			jsonConfig.SetAs("SpawnProtectionRangeZ-", SpawnProtectionRangeZNeg);
			jsonConfig.SetAs("BannerProtectionRangeX", BannerProtectionRangeX);
			jsonConfig.SetAs("BannerProtectionRangeZ", BannerProtectionRangeZ);
			jsonConfig.SetAs("NpcKillsKickThreshold", NpcKillsKickThreshold);
			jsonConfig.SetAs("NpcKillsBanThreshold", NpcKillsBanThreshold);
			jsonConfig.SetAs("NpcKillsJailThreshold", NpcKillsJailThreshold);
			jsonConfig.SetAs("ColonistLimit", ColonistLimit);
			jsonConfig.SetAs("ColonistCheckInterval", ColonistLimitCheckSeconds);
			jsonConfig.SetAs("ColonistLimitMaxKillPerIteration", ColonistLimitMaxKillPerIteration);
			jsonConfig.SetAs("OnlineBackupIntervalHours", OnlineBackupIntervalHours);

			// colonists per player limits (tier based)
			JSONNode jsonCapacityTiers = new JSONNode(NodeType.Array);
			for (int i = 0; i < ColonistTierLimits.Count; i++) {
				JSONNode node = new JSONNode();
				node.SetAs<int>(ColonistTierLimits[i]);
				jsonCapacityTiers.AddToArray(node);
			}
			jsonConfig.SetAs("ColonistCapacityTiers", jsonCapacityTiers);

			// colonists per colony limits (tier based)
			JSONNode jsonColonistTiers = new JSONNode(NodeType.Array);
			for (int i = 0; i < ColonistPerColonyTierLimits.Count; i++) {
				JSONNode tierSettings = new JSONNode(NodeType.Array);
				for (int j = 0; j < ColonistPerColonyTierLimits[i].Count; j++) {
					JSONNode jsonLimitSetting = new JSONNode();
					jsonLimitSetting.SetAs<int>(ColonistPerColonyTierLimits[i][j]);
					tierSettings.AddToArray(jsonLimitSetting);
				}
				jsonColonistTiers.AddToArray(tierSettings);
			}
			jsonConfig.SetAs("ColonistLimitsColonyDifficultyTiers", jsonColonistTiers);
			jsonConfig.SetAs("ColonyColonistLimitTierCheckSeconds", ColonyColonistLimitTierCheckSeconds);
			jsonConfig.SetAs("ColonyColonistLimitTierWarnTimes", ColonyColonistLimitTierWarnTimes);

			var jsonCustomAreas = new JSONNode (NodeType.Array);
			foreach (var customArea in CustomAreas) {
				jsonCustomAreas.AddToArray (customArea.ToJSON ());
			}
			jsonConfig.SetAs ("CustomAreas", jsonCustomAreas);
			jsonConfig.SetAs("DefaultWarpRange", TravelManager.DefaultWarpRange);
			jsonConfig.SetAs("WarDuration", WarDuration);
			jsonConfig.SetAs("StartupGracePeriod", StartupGracePeriod);

			JSON.Serialize (ConfigFilepath, jsonConfig, 2);
		}

		// track NPC killing
		[ModLoader.ModCallback(ModLoader.EModCallbackType.OnNPCHit, NAMESPACE + ".OnNPCHit")]
		public static void OnNPCHit(NPC.NPCBase npc, ModLoader.OnHitData data)
		{
			if (!IsKilled(npc, data) || !IsHitByPlayer(data.HitSourceType) || !(data.HitSourceObject is Players.Player)) {
				return;
			}
			Players.Player killer = (Players.Player)data.HitSourceObject;
			foreach (Players.Player owner in npc.Colony.Owners) {
				if (owner == killer) {
					return;
				}
			}

			// WAR mode: killing NPC is allowed if killer and target colony are war enabled
			if (WarManager.IsWarEnabled(killer) && WarManager.IsWarEnabled(npc.Colony)) {
				return;
			}

			int kills;
			if (!KillCounter.TryGetValue(killer, out kills)) {
				kills = 0;
			}
			KillCounter[killer] = ++kills;
			if (NpcKillsBanThreshold > 0 && kills >= NpcKillsBanThreshold) {
				Chat.SendToConnected($"{killer.Name} banned for killing too many colonists");
				BlackAndWhitelisting.AddBlackList(killer.ID.steamID.m_SteamID);
				Players.Disconnect(killer);
			} else if (NpcKillsKickThreshold > 0 && kills >= NpcKillsKickThreshold) {
				Chat.SendToConnected($"{killer.Name} kicked for killing too many colonists");
				Players.Disconnect(killer);
			} else if (NpcKillsJailThreshold > 0 && kills >= NpcKillsJailThreshold) {
				Chat.SendToConnected($"{killer.Name} put in Jail for killing too many colonists");
				JailManager.jailPlayer(killer, null, "Killing Colonists", JailManager.DEFAULT_JAIL_TIME);
			}
			Log.Write($"{killer.Name} killed a colonist of {npc.Colony.Name} at {npc.Position}");
			int remainingJail = NpcKillsJailThreshold - kills;
			int remainingKick = NpcKillsKickThreshold - kills;
			string msg = "You killed a colonist";
			if (NpcKillsJailThreshold > 0) {
				msg += $", remaining until jail: {remainingJail}";
			}
			if (NpcKillsKickThreshold > 0) {
				msg += $", remaining until kick: {remainingKick}";
			}
			Chat.Send(killer, msg);
		}

		static bool IsKilled(NPC.NPCBase npc, ModLoader.OnHitData data)
		{
			return npc.health - data.ResultDamage <= 0;
		}

		static bool IsHitByPlayer(ModLoader.OnHitData.EHitSourceType hitSourceType)
		{
			return hitSourceType == ModLoader.OnHitData.EHitSourceType.PlayerClick ||
				hitSourceType == ModLoader.OnHitData.EHitSourceType.PlayerProjectile ||
				hitSourceType == ModLoader.OnHitData.EHitSourceType.Misc;
		}

		// check colonist limit (total colonists per player)
		public static void CheckColonistLimit()
		{
			if (ColonistLimit < 1) {
				return;
			}

			int total_killed = 0;
			foreach (Players.Player target in Players.PlayerDatabase.Values) {
				if (target.Colonies == null || target.Colonies.Length == 0 || total_killed > ColonistLimitMaxKillPerIteration) {
					continue;
				}
				int player_colonists = 0;
				int killed_per_player = 0;
				foreach (Colony checkColony in target.Colonies) {
					if (checkColony.Owners[0] == target) {
						player_colonists += checkColony.FollowerCount;
					}
				}

				// calculate effective limit to allow tier levels per player
				int effectiveLimit = ColonistLimit;
				for (int i = 0; i < ColonistTierLimits.Count; i++) {
					if (ColonistTierLimits[i] > 0 && PermissionsManager.HasPermission(target, $"colonistcapacity.tier{i+1}")) {
						effectiveLimit = ColonistTierLimits[i];
					}
				}
				if (player_colonists <= effectiveLimit) {
					continue;
				}

				for (int i = target.Colonies.Length - 1; i >= 0; i--) {
					Colony colony = target.Colonies[i];

					if (colony.JobFinder.AutoRecruit) {
						JobFinder colonyJobFinder = colony.JobFinder;
						colonyJobFinder.AutoRecruit = false;
					}

					int killed_per_colony = 0;
					List<NPCBase> cachedFollowers = new List<NPCBase>(colony.Followers);
					int j = cachedFollowers.Count - 1;
					while (player_colonists > effectiveLimit && total_killed < ColonistLimitMaxKillPerIteration && j >= 0) {
						cachedFollowers[j--].OnDeath();
						player_colonists--;
						killed_per_colony++;
						killed_per_player++;
						total_killed++;
					}
					if (killed_per_colony > 0) {
						Log.Write($"ColonyCap: killed {killed_per_colony} colonists of {target.Name} in colony {colony.Name}. Player total: {player_colonists} (limit: {effectiveLimit})");
					}
				}
				if (target.ConnectionState == Players.EConnectionState.Connected) {
					Chat.Send(target, $"<color=red>Colonists are dying, limit is {effectiveLimit}</color>");
				}
			}

			if (total_killed > 0) {
				Log.Write($"ColonyCap: killed {total_killed} colonists in total");
			}

			ThreadManager.InvokeOnMainThread(delegate() {
				CheckColonistLimit();
			}, ColonistLimitCheckSeconds + 0.150);
		}

		public static void PerformColonistPerColonyLimitCheck()
		{
			int total_killed = 0;
			foreach (Colony colony in ServerManager.ColonyTracker.ColoniesByID.Values) {
				if (colony.Followers.Count == 0) {
					continue;
				}
				byte zombieDayIndex = ServerManager.WorldSettingsReadOnly.DifficultyDayMonsters;
				byte zombieNightIndex = ServerManager.WorldSettingsReadOnly.DifficultyNightMonsters;
				// bool happiness = ServerManager.WorldSettingsReadOnly.EnableHappiness;
				JSONNode node = new JSONNode(NodeType.Object).SetAs("difficulty", "1");
				Difficulty.ColonyDifficultySetting setting = (Difficulty.ColonyDifficultySetting)colony.DifficultySetting;
				if (setting != null) {
					setting.SerializeToColonyJSON(node, colony);
					node = node.GetAsOrDefault<JSONNode>("difficulty", null);
					if (node != null) {
						zombieDayIndex = node.GetAs<byte>("day_cd");
						zombieNightIndex = node.GetAs<byte>("night_cd");
						// happiness = node.GetAs<bool>("enablehappiness");
					}
				}
				int difficultyIndex = zombieDayIndex + zombieNightIndex;

				// find the correct tier limits by leader player
				Players.Player owner = colony.Owners[0];
				List<int> difficultyLimits = null;
				for (int i = 0; i < ColonistPerColonyTierLimits.Count; i++) {
					if (PermissionsManager.HasPermission(owner, $"colonistcapacity.tier{i+1}")) {
						difficultyLimits = ColonistPerColonyTierLimits[i];
					}
				}
				if (difficultyLimits == null || difficultyLimits.Count == 0) {
					continue;
				}

				// find the colonist limit based on difficulty
				int effectiveLimit = 0;
				if (difficultyIndex >= difficultyLimits.Count) {
					effectiveLimit = difficultyLimits[difficultyLimits.Count - 1];
				} else {
					effectiveLimit = difficultyLimits[difficultyIndex];
				}
				if (effectiveLimit == 0) {
					continue;
				}

				if (colony.Followers.Count <= effectiveLimit) {
					// remove from 'warning' list if below limit
					if (ColonyColonistLimitTiers.ContainsKey(colony)) {
						ColonyColonistLimitTiers.Remove(colony);
					}
					continue;
				}

				// above limit. send out warnings at first
				Log.Write($"Colony {colony.Name} is at {colony.Followers.Count} colonists. Limit is {effectiveLimit} for difficulty {difficultyIndex}");
				if (!ColonyColonistLimitTiers.ContainsKey(colony)) {
					ColonyColonistLimitTiers[colony] = 0;
				}
				int warnCount = ColonyColonistLimitTiers[colony];
				if (warnCount < ColonyColonistLimitTierWarnTimes && owner.ConnectionState == Players.EConnectionState.Connected) {
					Chat.Send(owner, $"<color=yellow>{colony.Name} is above the colonist limit ({effectiveLimit}) for your difficulty setting. Please increase zombie spawn level!</color>");
					ColonyColonistLimitTiers[colony] += 1; // inc warning count
				} else {
					Chat.Send(owner, $"<color=red>{colony.Name} is still above the colonist limit ({effectiveLimit}) for your difficulty setting. Killing colonists.</color>");
					int killed = 0;
					List<NPCBase> cachedFollowers = new List<NPCBase>(colony.Followers);
					int j = cachedFollowers.Count - 1;
					while (colony.Followers.Count > effectiveLimit && total_killed < ColonistLimitMaxKillPerIteration) {
						cachedFollowers[j--].OnDeath();
						killed++;
						total_killed++;
					}
					Log.Write($"Killed {killed} colonist from {colony.Name}, owner is {owner.Name}");

					if (colony.Followers.Count <= effectiveLimit) {
						ColonyColonistLimitTiers.Remove(colony);
					}
				}
			}
			Log.Write($"Colony difficulty/tier limit check: killed {total_killed} in total");

			// queue the next iteration
			ThreadManager.InvokeOnMainThread(delegate {
				PerformColonistPerColonyLimitCheck();
			}, ColonyColonistLimitTierCheckSeconds);
		}

		[ModLoader.ModCallback (ModLoader.EModCallbackType.OnAutoSaveWorld, NAMESPACE + ".OnAutoSaveWorld")]
		public static void OnAutoSaveWorld()
		{
			Save();
		}

		[ModLoader.ModCallback (ModLoader.EModCallbackType.OnQuit, NAMESPACE + ".OnQuit")]
		public static void OnQuit()
		{
			Save();
		}

		public static void PerformOnlineBackup()
		{
			double timeStart = Pipliz.Time.SecondsSinceStartDouble;
			Chat.SendToConnected("Starting online backup", EChatSendOptions.Default);

			string backupPath = "gamedata/savegames/" + ServerManager.WorldName + "-" + Pipliz.Time.FullTimeStamp();
			int num = 0;
			string text = backupPath + ".zip";
			while (File.Exists(text)) {
				num++;
				text = backupPath + $"-{num:02}.zip";
			}
			backupPath = text;

			ModLoader.Callbacks.OnAutoSaveWorld.Invoke();
			ServerManager.SaveManager.FlushAllDirtyChunks();
			ServerManager.SaveManager.EnqueueJob(new SaveManager.SaveJob(delegate (SaveManager.ChunkStorage storage) {
				Pipliz.Application.WaitForQuitsNoLogging();
				storage.FlushChunksToFreeForced();
				storage.Close();
				ZipFile.CreateFromDirectory("gamedata/savegames/" + ServerManager.WorldName, backupPath, CompressionLevel.Optimal, true);
				Chat.SendToConnected ("Backup complete", EChatSendOptions.Default);
			}));
			double secondsSinceStartDouble = Pipliz.Time.SecondsSinceStartDouble;
			Log.Write($"Online Backup completed; took {secondsSinceStartDouble - timeStart:F3} seconds");

			// queue the next iteration
			ThreadManager.InvokeOnMainThread(delegate {
				PerformOnlineBackup();
			}, OnlineBackupIntervalHours * 60f * 60f);
		}

		// load hook into AngryGuards mod, if it is available
		[ModLoader.ModCallback(ModLoader.EModCallbackType.AfterModsLoaded, NAMESPACE + ".AfterModsLoaded")]
		public static void AfterModsLoaded(List<ModLoader.ModDescription> mods)
		{
			Assembly angryguards = null;
			for (int i = 0; i < mods.Count; i++) {
				if (mods[i].name == "Angry Guards") {
					angryguards = mods[i].LoadedAssembly;
					Log.Write("ColonyCommands: found AngryGuards mod, enabling hook");
				}
			}

			if (angryguards == null) {
				return;
			}

			foreach (Type t in angryguards.GetTypes()) {
				if (t.FullName == "AngryGuards.AngryGuards") {
					MethodInfo m = t.GetMethod("ColonySetWarMode");
					if (m != null) {
						Log.Write("Method AngryGuards.ColonySetWarMode found, hook enabled");
						AngryGuardsWarMode = m;
					}
				}
			}

		}

		[ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerConnectedLate, NAMESPACE + ".OnPlayerConnectedLate")]
		public static void OnPlayerConnectedLate(Players.Player player)
	  	{
			Chat.Send(player, "<color=yellow>Anti-Grief protection enabled</color>");

			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond / 1000;
			if (ServerStartupTime + StartupGracePeriod < now) {
				return;
			}
			if (PermissionsManager.HasPermission(player, "antigrief.graceperiod")) {
				return;
			}
			Chat.Send(player, $"Server not yet ready. Please try again in {StartupGracePeriod/60} minutes");
			Players.Disconnect(player);
		}

	} // class

	// Helper function to save some lines of code
	public static class Helper
	{
		public static void TeleportPlayer(Players.Player target, Vector3 position, bool force = false)
		{
			// avoid teleporting while mounted
			if (MeshedObjectManager.HasVehicle(target)) {
				if (!force) {
					Chat.Send(target, "Please dismount before teleporting");
					return;
				} else {
					MeshedObjectManager.Detach(target);
				}
			}

			using (ByteBuilder byteBuilder = ByteBuilder.Get()) {
				byteBuilder.Write(ClientMessageType.ReceivePosition);
				byteBuilder.Write(position);
				NetworkWrapper.Send(byteBuilder, target);
			}
		}
	}

} // namespace

