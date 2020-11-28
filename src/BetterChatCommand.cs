using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Pipliz;
using Pipliz.JSON;
using Chatting;
using Chatting.Commands;

/*
 * Inspired by Crone's BetterChat
 */
namespace ColonyCommands
{
	[ModLoader.ModManager]
	public class BetterChatCommand : IChatCommand
	{
		public bool TryDoCommand(Players.Player causedBy, string chat, List<string> splits)
		{
			MuteList.Update();
			if (MuteList.MutedMinutes.ContainsKey(causedBy.ID)) {
				Chat.Send(causedBy, "[muted]");
				return true;
			}

			if (chat.StartsWith("/")) {
				return false;
			}

			// figure out chat color group name
			string groupname = null;
			foreach (string s in ChatColors.Colors.Keys) {
				if (PermissionsManager.HasPermission(causedBy, AntiGrief.MOD_PREFIX + "betterchat." + s)) {
					groupname = s;
					break;
				}
			}
			if (string.IsNullOrEmpty(groupname)) {
				Chat.SendToConnected(causedBy.Name + "> " + chat);
				return true;
			}
			ChatColorSpecification spec = ChatColors.Colors[groupname];
			
			string fulltext = "";
			if (!string.IsNullOrEmpty(spec.Prefix)) {
				if (!string.IsNullOrEmpty(spec.PrefixColor)) {
					fulltext = $"[<color={spec.PrefixColor}>{spec.Prefix}</color>]";
				} else {
					fulltext = $"[{spec.Prefix}]";
				}
			}
			if (!string.IsNullOrEmpty(spec.Name)) {
					fulltext += $"<color={spec.Name}>{causedBy.Name}</color>> ";
			} else {
				fulltext += causedBy.Name + "> ";
			}
			if (!string.IsNullOrEmpty(spec.Text)) {
					fulltext += $"<color={spec.Text}>{chat}</color>";
			} else {
				fulltext += chat;
			}

			Chat.SendToConnected(fulltext);
			return true;
		}
	}

	public static class ChatColors
	{
		public static Dictionary<string, ChatColorSpecification> Colors = new Dictionary<string, ChatColorSpecification>();

		private static string ConfigFilepath {
			get {
				return Path.Combine(Path.Combine("gamedata", "savegames"), Path.Combine(ServerManager.WorldName, "chatcolors.json"));
			}
		}

		// Load from config file (JSON)
		public static void LoadChatColors()
		{
			if (!File.Exists(ConfigFilepath)) {
				Log.Write($"Chatcolors file not found {ConfigFilepath}");
				return;
			}

			try {
				JSONNode jsonConfig;
				if (!JSON.Deserialize(ConfigFilepath, out jsonConfig, false)) {
					Log.WriteError($"Error loading {ConfigFilepath}");
					return;
				}
				JSONNode jsonColors;
				if (jsonConfig.TryGetAs("chatcolorgroups", out jsonColors) && jsonColors.NodeType == NodeType.Array) {
					foreach (JSONNode jGroup in jsonColors.LoopArray()) {
						string groupname, prefix, prefixcolor, name, text;
						jGroup.TryGetAs("groupname", out groupname);
						jGroup.TryGetAs("prefix", out prefix);
						jGroup.TryGetAs("prefixcolor", out prefixcolor);
						jGroup.TryGetAs("name", out name);
						jGroup.TryGetAs("text", out text);

						if (string.IsNullOrEmpty(groupname)) {
							continue;
						}
						ChatColorSpecification spec = new ChatColorSpecification(prefix, prefixcolor, name, text);
						Colors[groupname] = spec;
					}
				} else {
					Log.WriteError($"No 'chatcolorgroups' array found in {ConfigFilepath}");
				}
			} catch (Exception exception) {
				Log.WriteError($"Exception while loading chatcolors: {exception.Message}");
			}
		}
	}

	public struct ChatColorSpecification
	{
		public string Prefix;
		public string PrefixColor;
		public string Name;
		public string Text;

		public ChatColorSpecification(string p, string pc, string n, string t)
		{
			Prefix = p;
			PrefixColor = pc;
			Name = n;
			Text = t;
		}
	}
}

