using log4net;
using statsd.net.core.Structures;
using statsd.net.shared;
using statsd.net.shared.Messages;
using statsd.net.shared.Services;
using statsd.net.shared.Structures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using statsd.net.Configuration;

namespace statsd.net.Framework
{
  public class TimedGaugeAggregatorBlockFactory
  {
    public static ActionBlock<StatsdMessage> CreateBlock(ITargetBlock<Bucket> target,
      string rootNamespace, 
      bool removeZeroGauges,
      IEnumerable<ExtensionConfiguration.DynamicSource> dynamicSources,
      IIntervalService intervalService,
      ILog log)
    {
      var gauges = new ConcurrentDictionary<Tuple<string, string>, double>();
      var root = rootNamespace;
      var ns = String.IsNullOrEmpty(rootNamespace) ? "" : rootNamespace + ".";

      var incoming = new ActionBlock<StatsdMessage>(p =>
        {
          var gauge = p as Gauge;

          gauges.AddOrUpdate(new Tuple<string, string>(gauge.Name, gauge.Source), gauge.Value, (key, oldValue) => gauge.Value);
        },
        Utility.UnboundedExecution());

      var dynamicSourcesList = dynamicSources.ToList();

      intervalService.Elapsed += (sender, e) =>
        {
          if (gauges.Count == 0)
          {
            return;
          }

          if (removeZeroGauges)
          {
            var toErase = new List<Tuple<string, string>>();
            // Get all zero-value gauges
            var zeroGauges = 0;
            foreach (var entry in gauges)
            {
              if (entry.Value == 0.0)
              {
                toErase.Add(entry.Key);
                zeroGauges += 1;
              }
            }
            foreach (var eraseKey in toErase)
            {
              double placeholder;
              gauges.TryRemove(eraseKey, out placeholder);
            }
            if (zeroGauges > 0)
            {
              log.InfoFormat("Removed {0} empty gauges.", zeroGauges);
            }
          }

          GaugesBucket bucket;

          // If there are dynamic sources in configuration, payload will be selectively assembled:
          if (dynamicSources != null)
          {
            var payload = new List<KeyValuePair<Tuple<string, string>, double>>();

            foreach (var nameGroup in gauges.GroupBy(c => c.Key.Item1))
            {
              IEnumerable<KeyValuePair<Tuple<string, string>, double>> single = nameGroup;
              var dyn = DynamicSourceHelper.IsThisDynamicSource(nameGroup.Key, dynamicSourcesList);
              if (dyn != null)
              {
                single = DynamicSourceHelper.GetRankedSources(nameGroup, dyn.Ranking, dyn.Keep).ToList();
              }
              payload.AddRange(single);
            }

            bucket = new GaugesBucket(payload.ToArray(), e.Epoch, ns);
          }
          // Without dynamic sources, payload is everything that is currently in counters:
          else
          {
            bucket = new GaugesBucket(gauges.ToArray(), e.Epoch, ns);
          }
          
          gauges.Clear();
          target.Post(bucket);
        };

      incoming.Completion.ContinueWith(p =>
        {
          // Tell the upstream block that we're done
          target.Complete();
        });
      return incoming;
    }
  }
}
