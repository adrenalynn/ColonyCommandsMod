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
	public class BetterChatCommand : IChatCommand
	{
		public bool TryDoCommand(Players.Player causedBy, string chatmsg, List<string> splits)
		{
			// never match actual commands
			if (chatmsg.StartsWith("/")) {
				return false;
			}

			MuteList.Update();
			if (MuteList.MutedMinutes.ContainsKey(causedBy.ID)) {
				Chat.Send(causedBy, "<color=yellow>[muted]</color>");
				return true;
			}

			string Name = causedBy.Name;
			string Prefix = "";
			string Text = chatmsg;

			// chat colors
			ChatColors.ApplyColor(causedBy, ref Name, ref Prefix, ref Text);

			// roleplay marker
			if (RoleplayManager.IsRoleplaying(causedBy)) {
				Prefix = $"{Prefix}<color=yellow>[RP]</color>";
			}

			Chat.SendToConnected($"{Name}{Prefix}> {Text}");
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


		// apply color to a chat message
		public static void ApplyColor(Players.Player causedBy, ref string Name, ref string Prefix, ref string Msg)
		{
			// figure out chat color group
			ChatColorSpecification spec = default(ChatColorSpecification);
			bool found = false;
			foreach (string s in Colors.Keys) {
				if (PermissionsManager.HasPermission(causedBy, AntiGrief.MOD_PREFIX + "betterchat." + s)) {
					spec = Colors[s];
					found = true;
					break;
				}
			}
			if (!found) {
				return;
			}

			// Group Prefix
			if (!string.IsNullOrEmpty(spec.Prefix)) {
				if (!string.IsNullOrEmpty(spec.PrefixColor)) {
					Prefix = $"[<color={spec.PrefixColor}>{spec.Prefix}</color>]{Prefix}";
				} else {
					Prefix = $"[{spec.Prefix}]{Prefix}";
				}
			}

			// Name
			if (!string.IsNullOrEmpty(spec.Name)) {
				Name = $"<color={spec.Name}>{Name}</color>";
			}

			// Text
			if (!string.IsNullOrEmpty(spec.Text)) {
				Msg = $"<color={spec.Text}>{Msg}</color>";
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
						string groupname, prefix, prefixcolor, name, text, rpmarker;
						jGroup.TryGetAs("groupname", out groupname);
						jGroup.TryGetAs("prefix", out prefix);
						jGroup.TryGetAs("prefixcolor", out prefixcolor);
						jGroup.TryGetAs("name", out name);
						jGroup.TryGetAs("text", out text);
						jGroup.TryGetAsOrDefault("rpmarker", out rpmarker, "yellow");

						if (string.IsNullOrEmpty(groupname)) {
							continue;
						}
						ChatColorSpecification spec = new ChatColorSpecification(prefix, prefixcolor, name, text, rpmarker);
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
		public string RpMarker;

		public ChatColorSpecification(string p, string pc, string n, string t, string r)
		{
			Prefix = p;
			PrefixColor = pc;
			Name = n;
			Text = t;
			RpMarker = r;
		}
	}
}

