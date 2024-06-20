using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class DeephavenStateManager {
  public static readonly DeephavenStateManager Instance = new();

  private readonly Lender<Client> _clientLender = new(1);

  public void Connect() {

  }

  public void Reconnect() {

  }

  public IExcelObservable SnapshotTable(string tableName, TableFilter filter) {
    Debug.WriteLine("making another one why");
    return new SnapshotHandler(_clientLender, tableName, filter);
  }
}
