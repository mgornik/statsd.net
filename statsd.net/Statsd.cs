﻿using System.Configuration;
using System.Windows.Markup;
using statsd.net.Backends;
using statsd.net.core;
using statsd.net.core.Backends;
using statsd.net.core.Structures;
using statsd.net.shared.Listeners;
using statsd.net.shared.Messages;
using statsd.net.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using statsd.net.shared.Services;
using log4net;
using statsd.net.Backends.SqlServer;
using statsd.net.shared;
using statsd.net.shared.Factories;
using statsd.net.shared.Structures;
using statsd.net.Backends.Librato;
using statsd.net.Configuration;
using statsd.net.Backends.Statsdnet;

namespace statsd.net
{
    public class Statsd
    {
        private TransformBlock<string, StatsdMessage> _messageParser;
        private StatsdMessageRouterBlock _router;
        private BroadcastBlock<Bucket> _messageBroadcaster;
        private List<IBackend> _backends;
        private List<IListener> _listeners;
        private CancellationTokenSource _tokenSource;
        private ManualResetEvent _shutdownComplete;
        private static readonly ILog _log = LogManager.GetLogger("statsd.net");

        public WaitHandle ShutdownWaitHandle
        {
            get
            {
                return _shutdownComplete;
            }
        }

        public Statsd(string serviceName = null)
        {
            _log.Info("statsd.net starting.");
            _tokenSource = new CancellationTokenSource();
            _shutdownComplete = new ManualResetEvent(false);

            SuperCheapIOC.Add(_log);
            var systemInfoService = new SystemInfoService();
            SuperCheapIOC.Add(systemInfoService as ISystemInfoService);
            serviceName = serviceName ?? systemInfoService.HostName;
            var systemMetricsService = new SystemMetricsService("statsdnet", serviceName);
            SuperCheapIOC.Add(systemMetricsService as ISystemMetricsService);

            /**
             * The flow is:
             *  Listeners ->
             *    Message Parser ->
             *      router ->
             *        Aggregator ->
             *          Broadcaster ->
             *            Backends
             */

            // Initialise the core blocks
            _router = new StatsdMessageRouterBlock();
            _messageBroadcaster = new BroadcastBlock<Bucket>(Bucket.Clone);
            _messageBroadcaster.Completion.LogAndContinueWith(_log, "MessageBroadcaster", () =>
              {
                  _log.Info("MessageBroadcaster: Completion signaled. Notifying all backends.");
                  _backends.ForEach(q => q.Complete());
              });

            // Add the broadcaster to the IOC container
            SuperCheapIOC.Add<BroadcastBlock<Bucket>>(_messageBroadcaster);
            systemMetricsService.SetTarget(_messageBroadcaster);

            _backends = new List<IBackend>();
            _listeners = new List<IListener>();
        }

        public Statsd(StatsdnetConfiguration config)
            : this(config.Name)
        {
            _log.Info("statsd.net loading config.");
            var systemMetrics = SuperCheapIOC.Resolve<ISystemMetricsService>();
            systemMetrics.HideSystemStats = config.HideSystemStats;

            LoadMessageParser(config);
            
            LoadBackends(config, systemMetrics);

            // Load Aggregators
            var intervalServices = new List<IIntervalService>();
            var intervalService = new IntervalService(config.FlushInterval,
              _tokenSource.Token);
            intervalServices.Add(intervalService);
            LoadAggregators(config,
              intervalService,
              _messageBroadcaster,
              systemMetrics);
            // Load Listeners
            LoadListeners(config, _tokenSource.Token, systemMetrics);

            // Now start the interval service
            intervalServices.ForEach(p => p.Start());

            // Announce that we've started
            systemMetrics.LogCount("started");
        }

        private void LoadAggregators(StatsdnetConfiguration config,
          IntervalService intervalService,
          BroadcastBlock<Bucket> messageBroadcaster,
          ISystemMetricsService systemMetrics)
        {
            foreach (var aggregator in config.Aggregators)
            {
                switch (aggregator.Key)
                {
                    case "counters":
                        var counter = aggregator.Value as CounterAggregationConfig;
                        AddAggregator(MessageType.Counter,
                          TimedCounterAggregatorBlockFactory.CreateBlock(messageBroadcaster,
                            counter.Namespace,
                            intervalService,
                            _log),
                          systemMetrics);
                        break;
                    case "gauges":
                        var gauge = aggregator.Value as GaugeAggregatorConfig;
                        AddAggregator(MessageType.Gauge,
                          TimedGaugeAggregatorBlockFactory.CreateBlock(messageBroadcaster,
                            gauge.Namespace,
                            gauge.RemoveZeroGauges,
                            intervalService,
                            _log),
                          systemMetrics);
                        break;
                    case "calendargrams":
                        var calendargram = aggregator.Value as CalendargramAggregationConfig;
                        AddAggregator(MessageType.Calendargram,
                            TimedCalendargramAggregatorBlockFactory.CreateBlock(messageBroadcaster,
                                calendargram.Namespace,
                                intervalService,
                                new TimeWindowService(),
                                _log),
                                systemMetrics);
                        break;
                    case "timers":
                        var timer = aggregator.Value as TimersAggregationConfig;
                        AddAggregator(MessageType.Timing,
                          TimedLatencyAggregatorBlockFactory.CreateBlock(messageBroadcaster,
                            timer.Namespace,
                            intervalService,
                            timer.CalculateSumSquares,
                            _log),
                          systemMetrics);
                        // Add Percentiles
                        foreach (var percentile in timer.Percentiles)
                        {
                            AddAggregator(MessageType.Timing,
                              TimedLatencyPercentileAggregatorBlockFactory.CreateBlock(messageBroadcaster,
                                timer.Namespace,
                                intervalService,
                                percentile.Threshold,
                                percentile.Name,
                                _log),
                              systemMetrics);
                        }
                        break;

                }
            }
            // Add the Raw (pass-through) aggregator
            AddAggregator(MessageType.Raw,
              PassThroughBlockFactory.CreateBlock(messageBroadcaster, intervalService),
              systemMetrics);
        }

        private void LoadBackends(StatsdnetConfiguration config, ISystemMetricsService systemMetrics)
        {
            foreach (var backend in config.GetConfiguredBackends(systemMetrics))
            {
                AddBackend(backend, systemMetrics, backend.Name);
            }
        }

        private void LoadMessageParser(StatsdnetConfiguration config)
        {
            _messageParser = MessageParserBlockFactory.CreateMessageParserBlock(_tokenSource.Token,
              SuperCheapIOC.Resolve<ISystemMetricsService>(),
              config.ExtensionConfiguration.NameAndSourceRegex,
              _log);
            _messageParser.LinkTo(_router);
            _messageParser.Completion.LogAndContinueWith(_log, "MessageParser", () =>
            {
                _log.Info("MessageParser: Completion signaled. Notifying the MessageBroadcaster.");
                _messageBroadcaster.Complete();
            });
        }

        private void LoadListeners(StatsdnetConfiguration config, 
            CancellationToken cancellationToken,
            ISystemMetricsService systemMetrics)
        {
            // Load listeners - done last and once the rest of the chain is in place
            foreach (var listenerConfig in config.Listeners)
            {
                if (listenerConfig is UDPListenerConfiguration)
                {
                    var udpConfig = listenerConfig as UDPListenerConfiguration;
                    AddListener(new UdpStatsListener(udpConfig.Port, systemMetrics));
                    systemMetrics.LogCount("startup.listener.udp." + udpConfig.Port);
                }
                else if (listenerConfig is TCPListenerConfiguration)
                {
                    var tcpConfig = listenerConfig as TCPListenerConfiguration;
                    AddListener(new TcpStatsListener(tcpConfig.Port, systemMetrics));
                    systemMetrics.LogCount("startup.listener.tcp." + tcpConfig.Port);
                }
                else if (listenerConfig is HTTPListenerConfiguration)
                {
                    var httpConfig = listenerConfig as HTTPListenerConfiguration;
                    AddListener(new HttpStatsListener(httpConfig.Port, systemMetrics));
                    systemMetrics.LogCount("startup.listener.http." + httpConfig.Port);
                }
                else if (listenerConfig is StatsdnetListenerConfiguration)
                {
                    var statsdnetConfig = listenerConfig as StatsdnetListenerConfiguration;
                    AddListener(new StatsdnetTcpListener(statsdnetConfig.Port, systemMetrics));
                    systemMetrics.LogCount("startup.listener.statsdnet." + statsdnetConfig.Port);
                }
                else if (listenerConfig is MSSQLRelayListenerConfiguration)
                {
                    var mssqlRelayConfig = listenerConfig as MSSQLRelayListenerConfiguration;
                    AddListener(new MSSQLRelayListener(mssqlRelayConfig.ConnectionString,
                        mssqlRelayConfig.PollInterval,
                        cancellationToken,
                        mssqlRelayConfig.BatchSize,
                        mssqlRelayConfig.DeleteAfterSend,
                        systemMetrics));
                }
            }
        }

        public void AddListener(IListener listener)
        {
            _log.InfoFormat("Adding listener {0}", listener.GetType().Name);
            _listeners.Add(listener);
            listener.LinkTo(_messageParser, _tokenSource.Token);
        }

        private void AddAggregator(MessageType targetType,
          ActionBlock<StatsdMessage> aggregator,
          ISystemMetricsService systemMetrics)
        {
            _router.AddTarget(targetType, aggregator);
            systemMetrics.LogCount("startup.aggregator." + targetType.ToString());
        }

        public void AddBackend(IBackend backend, ISystemMetricsService systemMetrics, string name)
        {
            _log.InfoFormat("Adding backend {0} named '{1}'", backend.GetType().Name, name);
            _backends.Add(backend);
            _messageBroadcaster.LinkTo(backend);
            backend.Completion.LogAndContinueWith(_log, name, () =>
              {
                  if (_backends.All(q => !q.IsActive))
                  {
                      _shutdownComplete.Set();
                  }
              });
            systemMetrics.LogCount("startup.backend." + name);
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            _shutdownComplete.WaitOne();
        }
    }
}
