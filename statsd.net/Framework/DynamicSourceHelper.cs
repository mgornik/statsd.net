using System;
using System.Collections.Generic;
using System.Linq;
using statsd.net.Configuration;

namespace statsd.net.Framework
{
  class DynamicSourceHelper
  {
    internal static ExtensionConfiguration.DynamicSource IsThisDynamicSource(string name, IEnumerable<ExtensionConfiguration.DynamicSource> dynamicSources)
    {
      return (from dynamicSource in dynamicSources
              where dynamicSource.PrecompiledRegex.Match(name).Success
              select dynamicSource).FirstOrDefault();
    }
    internal static IEnumerable<KeyValuePair<Tuple<string, string>, TValueType>> GetRankedSources<TValueType>(IEnumerable<KeyValuePair<Tuple<string, string>, TValueType>> enumerable, ExtensionConfiguration.DynamicSource.RankingOperation ranking, int keep)
    {
      IOrderedEnumerable<KeyValuePair<Tuple<string, string>, TValueType>> ranked;

      if (ranking == ExtensionConfiguration.DynamicSource.RankingOperation.Top)
      {
        ranked = enumerable.OrderByDescending(e => e.Value);
      }
      else if (ranking == ExtensionConfiguration.DynamicSource.RankingOperation.Bottom)
      {
        ranked = enumerable.OrderBy(e => e.Value);
      }
      else
      {
        throw new Exception("Unsupported ranking type: " + ranking);
      }

      return ranked.Take(keep);
    }
  }
}
