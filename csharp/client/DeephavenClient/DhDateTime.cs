using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient;

/// <summary>
/// Deephaven's custom representation of DateTime, with full nanosecond resolution,
/// unlike .NET's own System.DateTime which has 100ns resolution.
/// </summary>
public class DhDateTime {
  public readonly Int64 Nanos;

  public DhDateTime(Int64 nanos) => Nanos = nanos;

  public DateTime DateTime => DateTime.UnixEpoch.AddTicks(Nanos / 100);
}
