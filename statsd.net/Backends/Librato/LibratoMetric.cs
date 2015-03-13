using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace statsd.net.Backends.Librato
{
  public abstract class LibratoMetric
  {
    public LibratoMetricType MetricType { get; private set; }
    public long measure_time { get; private set; }
    public string source { get; private set; }
    public LibratoMetric(LibratoMetricType type, long epoch, string metricSource)
    {
      MetricType = type;
      measure_time = epoch;
      source = String.IsNullOrWhiteSpace(metricSource) ? null : metricSource;
    }
  }
}
