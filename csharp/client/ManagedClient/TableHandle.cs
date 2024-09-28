using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ManagedClient;

public class TableHandle : IDisposable {
  public extern void Dispose();

  /// <summary>
  /// Creates a new table from this table, but including the additional specified columns
  /// </summary>
  /// <param name="columnSpecs">The columnSpecs to add. For example, "X = A + 5", "Y = X * 2"</param>
  /// <returns>The TableHandle of the new table</returns>
  public extern TableHandle Update(params string[] columnSpecs);

  public extern string ToString(bool wantHeaders);
}
