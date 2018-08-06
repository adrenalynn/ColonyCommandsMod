﻿using System.Text.RegularExpressions;
using Pipliz.Chatting;
using ChatCommands;
using Permissions;

namespace ColonyCommands
{

  public class DeleteJobSpeedCommand : IChatCommand
  {
    public bool IsCommand(string chat)
    {
      return (chat.Equals("/deletejobspeed") || chat.StartsWith("/deletejobspeed "));
    }

    public bool TryDoCommand(Players.Player causedBy, string chattext)
    {

      string permission = AntiGrief.MOD_PREFIX + "deletespeedjobs";
      if (!PermissionsManager.CheckAndWarnPermission(causedBy, permission)) {
        return true;
      }

      var m = Regex.Match(chattext, @"/deletejobspeed (?<speed>\d+)$");
      if (!m.Success) {
        Chat.Send(causedBy, "Syntax error: /deletejobspeed <#blocks>");
        return true;
      }
      int new_speed;
      if (!int.TryParse(m.Groups["speed"].Value, out new_speed)) {
        Chat.Send(causedBy, "Parse error: /deletejobspeed <#blocks> - blocks can only be numbers");
      }
      int speed = DeleteJobsManager.GetDeleteJobSpeed();
      DeleteJobsManager.SetDeleteJobSpeed(new_speed);

      Chat.Send(causedBy, $"DeleteJobSpeed was updated from {speed} to {new_speed} blocks per second");

      return true;
    }

  }

}

