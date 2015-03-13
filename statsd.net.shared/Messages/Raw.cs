using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace statsd.net.shared.Messages
{
  public class Raw : StatsdMessage
  {
    public double Value { get; set; }
    public long? Timestamp { get; set; }

    public Raw(string name,
      string source,
      double value,
      long? timestamp = null)
    {
      MessageType = MessageType.Raw;
      Name = name;
      Source = source;
      Value = value;
      Timestamp = timestamp;
    }

    public override string ToString()
    {
      return String.Format("{0}:{1}|r{2}", Name, Value, (Timestamp.HasValue ? ("|" + Timestamp) : String.Empty));
    }
  }
}
