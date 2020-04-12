using System;
using System.IO;
using System.Collections.Generic;
using Pipliz;
using Pipliz.JSON;

namespace ColonyCommands {

	[ModLoader.ModManager]
	public static class TravelManager
	{ 
		const string CONFIG_FILE = "travelpaths.json";
		public static int DefaultWarpRange = 2;
		private static string Configfile {
			get {
				return Path.Combine(Path.Combine("gamedata", "savegames"), Path.Combine(ServerManager.WorldName, CONFIG_FILE));
			}
		}

		private static Dictionary<Players.Player, Vector3Int> warpedPlayers = new Dictionary<Players.Player, Vector3Int>();
		private static Dictionary<Vector3Int, Vector3Int> TravelPoints = new Dictionary<Vector3Int, Vector3Int>();

		// track players movement
		[ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerMoved, AntiGrief.NAMESPACE + ".OnPlayerMoved")]
		public static void OnPlayerMoved(Players.Player causedBy, UnityEngine.Vector3 pos)
		{
			// avoid warping loop. Player needs to move outside the warp range first
			if (warpedPlayers.ContainsKey(causedBy)) {
				if (Distance(causedBy.VoxelPosition, warpedPlayers[causedBy]) > DefaultWarpRange * 2 &&
					Distance(causedBy.VoxelPosition, TravelPoints[warpedPlayers[causedBy]]) > DefaultWarpRange * 2) {
					warpedPlayers.Remove(causedBy);
				}
				return;
			}

			// check if at a travel point
			foreach (Vector3Int point in TravelPoints.Keys) {
				if (Distance(causedBy.VoxelPosition, point) <= DefaultWarpRange) {
					warpedPlayers.Add(causedBy, point);
					Helper.TeleportPlayer(causedBy, TravelPoints[point].Vector);
					break;
				}
			}
		}

		// add a travel path
		public static bool addPath(Players.Player causedBy, Vector3Int source, Vector3Int target)
		{
			// check for duplicates
			foreach (Vector3Int point in TravelPoints.Keys) {
				if (Distance(point, source) < DefaultWarpRange * 2 ||
					Distance(point, target) < DefaultWarpRange * 2) {
					return false;
				}
			}

			// add two points, source->target and target->source
			Log.Write($"Adding travel path between {source} and {target}");
			TravelPoints.Add(source, target);
			TravelPoints.Add(target, source);
			Save();
			warpedPlayers.Add(causedBy, target);
			return true;
		}

		// remove a travel path
		public static bool removePath(Players.Player causedBy, Vector3Int pos)
		{
			Vector3Int source = new Vector3Int();
			bool found = false;
			foreach (Vector3Int point in TravelPoints.Keys) {
				if (Distance(point, pos) <= DefaultWarpRange * 2) {
					source = point;
					found = true;
					break;
				}
			}
			if (found) {
				Vector3Int target = TravelPoints[source];
				Log.Write($"Removing travel path between {source} and {target}");
				TravelPoints.Remove(target);
				TravelPoints.Remove(source);
				if (warpedPlayers.ContainsKey(causedBy)) {
					warpedPlayers.Remove(causedBy);
				}
				Save();
			}
			return found;
		}

		public static void Load()
		{
			JSONNode jsonConfig;
			if (!JSON.Deserialize(Configfile, out jsonConfig, false)) {
				Log.Write("No {0} found inside world directory", CONFIG_FILE);
				return;
			}

			Log.Write("Loading travel paths from {0}", CONFIG_FILE);
			foreach (JSONNode node in jsonConfig.LoopArray()) {
					JSONNode jsonSource = node["source"];
					Vector3Int source = new Vector3Int(
						jsonSource.GetAs<int>("x"),
						jsonSource.GetAs<int>("y"),
						jsonSource.GetAs<int>("z")
					);
					JSONNode jsonTarget = node["target"];
					Vector3Int target = new Vector3Int(
						jsonTarget.GetAs<int>("x"),
						jsonTarget.GetAs<int>("y"),
						jsonTarget.GetAs<int>("z")
					);
					TravelPoints.Add(source, target);
			}
		}

		// Save() is only run when a new travel point pair was added
		public static void Save()
		{
			Log.Write("Saving travel points to {0}", CONFIG_FILE);
			JSONNode jsonConfig = new JSONNode(NodeType.Array);

			foreach (Vector3Int point in TravelPoints.Keys) {
				JSONNode source = new JSONNode(NodeType.Object)
					.SetAs("x", point.x)
					.SetAs("y", point.y)
					.SetAs("z", point.z)
				;
				JSONNode target = new JSONNode(NodeType.Object)
					.SetAs("x", TravelPoints[point].x)
					.SetAs("y", TravelPoints[point].y)
					.SetAs("z", TravelPoints[point].z)
				;
				jsonConfig.AddToArray(new JSONNode(NodeType.Object)
					.SetAs("source", source)
					.SetAs("target", target)
				);
			}

			try {
				JSON.Serialize(Configfile, jsonConfig);
			} catch (Exception e) {
				Log.Write("Could not save {0}: {1}", CONFIG_FILE, e.Message);
			}

			return;
		}

		// calculate distance as int
		public static int Distance(Vector3Int a, Vector3Int b)
		{
			return (int)System.Math.Sqrt(System.Math.Pow(a.x - b.x, 2) + System.Math.Pow(a.y - b.y, 2) + System.Math.Pow(a.z - b.z, 2));
		}

	}	// class

} // namespace

