using log4net;
using statsd.net.core.Structures;
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
using System.Text.RegularExpressions;

namespace statsd.net.Framework
{
  public class TimedCounterAggregatorBlockFactory
  {
    public static ActionBlock<StatsdMessage> CreateBlock(ITargetBlock<Bucket> target,
      string rootNamespace, 
      IEnumerable<ExtensionConfiguration.DynamicSource> dynamicSources,
      IIntervalService intervalService,
      ILog log)
    {
      var counters = new ConcurrentDictionary<Tuple<string, string>, double>();
      var root = rootNamespace;
      var ns = String.IsNullOrEmpty(rootNamespace) ? "" : (rootNamespace + ".");

      var incoming = new ActionBlock<StatsdMessage>(p =>
        {
          var counter = p as Counter;
          counters.AddOrUpdate(new Tuple<string, string>(counter.Name, counter.Source), counter.Value, (key, oldValue) => oldValue + counter.Value);
        },
        new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

      var dynamicSourcesList = dynamicSources.ToList();
      
      intervalService.Elapsed += (sender, e) =>
        {
          if (counters.Count == 0)
          {
            return;
          }

          CounterBucket bucket;

          // If there are dynamic sources in configuration, payload will be selectively assembled:
          if (dynamicSources != null)
          {
            var payload = new List<KeyValuePair<Tuple<string, string>, double>>();

            foreach (var nameGroup in counters.GroupBy(c => c.Key.Item1))
            {
              IEnumerable<KeyValuePair<Tuple<string, string>, double>> single = nameGroup;
              var dyn = DynamicSourceHelper.IsThisDynamicSource(nameGroup.Key, dynamicSourcesList);
              if (dyn != null)
              {
                single = DynamicSourceHelper.GetRankedSources(nameGroup, dyn.Ranking, dyn.Keep).ToList();
              } 
              payload.AddRange(single);
            }

            bucket = new CounterBucket(payload.ToArray(), e.Epoch, ns);
          }
          // Without dynamic sources, payload is everything that is currently in counters:
          else
          {
            bucket = new CounterBucket(counters.ToArray(), e.Epoch, ns);
          }

          counters.Clear();
          target.Post(bucket);
        };

      incoming.Completion.ContinueWith(p =>
        {
          log.Info("TimedCounterAggregatorBlock completing.");
          // Tell the upstream block that we're done
          target.Complete();
        });
      return incoming;
    }
  }
}
