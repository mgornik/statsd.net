﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace statsd.net.shared.Messages
{
  public abstract class StatsdMessage
  {
    public string Name { get; set; }
    public string Source { get; set; }
    public MessageType MessageType { get; protected set; }
    
    public StatsdMessage()
    {
    }
  }
}
