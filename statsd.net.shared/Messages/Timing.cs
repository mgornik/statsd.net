using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace statsd.net.shared.Messages
{
  public sealed class Timing : StatsdMessage
  {
    public double ValueMS { get; set; }

    public Timing(string name, string source, double valueMS)
    {
      Name = name;
      Source = source;
      ValueMS = valueMS;
      MessageType = MessageType.Timing;
    }

    public override string ToString()
    {
      return String.Format("{0}:{1}|ms", Name, ValueMS);
    }
  }
}
