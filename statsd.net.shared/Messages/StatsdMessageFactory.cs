using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using log4net.Util;

namespace statsd.net.shared.Messages
{
  public static class StatsdMessageFactory
  {
    private static char[] splitter = new char[] { '|' };

    public static StatsdMessage ParseMessage(string line, string sourceAndNameRegex)
    {
      try
      {
        string[] nameAndValue = line.Split(':');
        if (nameAndValue[0].Length == 0)
        {
          return new InvalidMessage("Name cannot be empty.");
        }
        string[] statProperties = nameAndValue[1].Split(splitter, StringSplitOptions.RemoveEmptyEntries);
        if (statProperties.Length < 2)
        {
          return new InvalidMessage("Malformed message.");
        }

        var name = "";
        var source = "";

        if (!String.IsNullOrWhiteSpace(sourceAndNameRegex))
        {
          var regex = new Regex(sourceAndNameRegex);
          var match = regex.Match(nameAndValue[0]);
          if (match.Success)
          {
            source = match.Groups["source"].Value;
            name = match.Groups["name"].Value;
          }
          else
          {
            name = nameAndValue[0];
          }
        }

        switch (statProperties[1])
        {
          case "c":
            if (statProperties.Length == 2)
            {
              // gorets:1|c
              return new Counter(name, source, Double.Parse(statProperties[0]));
            }
            else
            {
              // gorets:1|c|@0.1
              return new Counter(name, source, Double.Parse(statProperties[0]), float.Parse(statProperties[2].Remove(0, 1)));
            }
          case "ms":
            // glork:320|ms
            return new Timing(name, source, Double.Parse(statProperties[0]));
          case "g":
            // gaugor:333|g
            return new Gauge(name, source, Double.Parse(statProperties[0]));
          case "s":
            // uniques:765|s
            // uniques:ABSA434As1|s
            return new Set(name, source, statProperties[0]);
          case "r":
            // some.other.value:12312|r
            // some.other.value:12312|r|99988883333
            if (statProperties.Length == 2)
            {
              return new Raw(name, source, Double.Parse(statProperties[0]));
            }
            else
            {
              return new Raw(name, source, Double.Parse(statProperties[0]), long.Parse(statProperties[2]));
            }
          case "cg":
            // calendargram.key:value|cg|{h,d,w,m,dow}
            return new Calendargram(name, source, statProperties[0], statProperties[2]);
          default:
            return new InvalidMessage("Unknown message type: " + statProperties[1]);
        }
      }
      catch (Exception ex)
      {
        return new InvalidMessage("Couldn't parse message: " + ex.Message);
      }
    }

    public static bool IsProbablyAValidMessage(string line)
    {
      if (String.IsNullOrWhiteSpace(line)) return false;
      string[] nameAndValue = line.Split(':');
      if (nameAndValue[0].Length == 0) return false;
      return true;
    }
  }
}
