using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ManagedClient;

public abstract class RowSequence {
  public static RowSequence CreateSequential(Int64 begin, Int64 end) {
    return new SequentialRowSequence(begin, end);
  }
}

public class SequentialRowSequence(Int64 begin, Int64 end) : RowSequence {
}
