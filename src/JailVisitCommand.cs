﻿using Chatting;
using Chatting.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ColonyCommands
{

  public class JailVisitCommand : IChatCommand
  {

    public bool TryDoCommand(Players.Player causedBy, string chattext, List<string> splits)
    {
		if (splits.Count == 0 || (!splits[0].Equals("/jailvisit") && !splits[0].Equals("/visitjail"))) {
			return false;
		}
      if (!JailManager.validVisitorPos) {
        Chat.Send(causedBy, "Found no valid jail visitor position");
        return true;
      }

      JailManager.VisitJail(causedBy);

      return true;
    }
  }
}

