using log4net;
using statsd.net.core.Structures;
using statsd.net.shared.Messages;
using statsd.net.shared.Services;
using statsd.net.shared.Structures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using statsd.net.Configuration;

namespace statsd.net.Framework
{
  public class TimedLatencyAggregatorBlockFactory
  {
    public static ActionBlock<StatsdMessage> CreateBlock(ITargetBlock<Bucket> target,
      string rootNamespace,
      IEnumerable<ExtensionConfiguration.DynamicSource> dynamicSources,
      IIntervalService intervalService,
      bool calculateSumSquares,
      ILog log,
      int maxItemsPerBucket = 1000)
    {
      var latencies = new ConcurrentDictionary<Tuple<string, string>, LatencyDatapointBox>();
      var root = rootNamespace;
      var ns = String.IsNullOrEmpty(rootNamespace) ? "" : rootNamespace + ".";
	  
      var incoming = new ActionBlock<StatsdMessage>( p =>
        {
          var latency = p as Timing;

          latencies.AddOrUpdate(new Tuple<string, string>(latency.Name, latency.Source),
              (key) =>
              {
                return new LatencyDatapointBox(maxItemsPerBucket, latency.ValueMS);
              },
              (key, bag) =>
              {
                bag.Add(latency.ValueMS);
                return bag;
              });
        },
        new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

      var dynamicSourcesList = dynamicSources.ToList();

      intervalService.Elapsed += (sender, e) =>
        {
          if (latencies.Count == 0)
          {
            return;
          }

          LatencyBucket bucket;

          // If there are dynamic sources in configuration, payload will be selectively assembled:
          if (dynamicSources != null)
          {
            var payload = new List<KeyValuePair<Tuple<string, string>, LatencyDatapointBox>>();

            foreach (var nameGroup in latencies.GroupBy(c => c.Key.Item1))
            {
              IEnumerable<KeyValuePair<Tuple<string, string>, LatencyDatapointBox>> single = nameGroup;
              var dyn = DynamicSourceHelper.IsThisDynamicSource(nameGroup.Key, dynamicSourcesList);
              if (dyn != null)
              {
                single = DynamicSourceHelper.GetRankedSources(nameGroup, dyn.Ranking, dyn.Keep).ToList();
              }
              payload.AddRange(single);
            }

            bucket = new LatencyBucket(payload.ToArray(), e.Epoch, ns, calculateSumSquares);
          }
          // Without dynamic sources, payload is everything that is currently in counters:
          else
          {
            bucket = new LatencyBucket(latencies.ToArray(), e.Epoch, ns, calculateSumSquares);
          }

          latencies.Clear();
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
