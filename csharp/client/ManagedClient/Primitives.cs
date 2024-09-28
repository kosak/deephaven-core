using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ManagedClient;

public class DurationSpecifier {
  private readonly object _duration;

  public DurationSpecifier(Int64 nanos) => _duration = nanos;
  public DurationSpecifier(string duration) => _duration = duration;

  public static implicit operator DurationSpecifier(Int64 nanos) => new(nanos);
  public static implicit operator DurationSpecifier(string duration) => new(duration);
  public static implicit operator DurationSpecifier(TimeSpan ts) => new((long)(ts.TotalMicroseconds * 1000));
}

public class TimePointSpecifier {
  private readonly object _timePoint;

  public TimePointSpecifier(Int64 nanos) => _timePoint = nanos;
  public TimePointSpecifier(string timePoint) => _timePoint = timePoint;

  public static implicit operator TimePointSpecifier(Int64 nanos) => new(nanos);
  public static implicit operator TimePointSpecifier(string timePoint) => new(timePoint);
}
