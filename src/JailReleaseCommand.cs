﻿using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

  public class JailReleaseCommand : IChatCommand
  {

    public bool IsCommand(string chat)
    {
      return (chat.Equals("/jailrelease") || chat.StartsWith("/jailrelease "));
    }

    public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
    {
      if (!PermissionsManager.CheckAndWarnPermission(causedBy, AntiGrief.MOD_PREFIX + "jail")) {
        return true;
      }

      var m = Regex.Match(chattext, @"/jailrelease (?<player>.+)");
      if (!m.Success) {
        Chat.Send(causedBy, "Syntax error, use /jailrelease <player>");
        return true;
      }

      Players.Player target;
      string targetName = m.Groups["player"].Value;
      string error;
      if (!PlayerHelper.TryGetPlayer(targetName, out target, out error, true)) {
        Chat.Send(causedBy, $"Could not find player {targetName}: {error}");
        return true;
      }

      if (!JailManager.IsPlayerJailed(target)) {
        Chat.Send(causedBy, $"{target.Name} is currently not in jail");
        return true;
      }

      JailManager.releasePlayer(target, causedBy);
      Chat.Send(causedBy, $"Released {target.Name} from jail");
      Chat.Send(target, $"<color=yellow>{causedBy.Name} released you from jail</color>");

      return true;
    }
  }
}

