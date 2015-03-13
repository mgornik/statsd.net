using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace statsd.net.Backends.Librato
{
  [DebuggerDisplay("{name} = {value}")]
  public class LibratoGauge : LibratoMetric
  {
    public string name { get; set; }
    public double value { get; set; }

    public LibratoGauge(string name, double value, long epoch, string source)
      : base(LibratoMetricType.Gauge, epoch, source)
    {
      this.name = Regex.Replace(name, LibratoBackend.ILLEGAL_NAME_CHARACTERS, "_");
      this.value = value;
    }
  }
}
