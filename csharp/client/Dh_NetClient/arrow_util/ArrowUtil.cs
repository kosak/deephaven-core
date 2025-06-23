using System.Collections;
using Apache.Arrow.Flight;
using Io.Deephaven.Proto.Backplane.Grpc;
using ArrowColumn = Apache.Arrow.Column;
using ArrowField = Apache.Arrow.Field;
using ArrowTable = Apache.Arrow.Table;
using IArrowType = Apache.Arrow.Types.IArrowType;

namespace Deephaven.Dh_NetClient;

public static class ArrowUtil {
  public static FlightDescriptor ConvertTicketToFlightDescriptor(Ticket ticket) {
    var bytes = ticket.Ticket_.Span;
    if (bytes.Length != 5 || bytes[0] != 'e') {
      throw new Exception("Ticket is not in correct format for export");
    }

    var value = BitConverter.ToUInt32(bytes.Slice(1));
    return FlightDescriptor.CreatePathDescriptor("export", value.ToString());
  }

  public static bool TypesEqual(IArrowType lhs, IArrowType rhs) {
    var dtc = new third_party.Apache.Arrow.ArrayDataTypeComparer(lhs);
    rhs.Accept(dtc);
    return dtc.DataTypeMatch;
  }

  public static ArrowTable ToArrowTable(IClientTable clientTable) {
    var ncols = clientTable.NumCols;
    var nrows = clientTable.NumRows;
    var columns = new List<ArrowColumn>();

    for (var i = 0; i != ncols; ++i) {
      var columnSource = clientTable.GetColumn(i);
      var arrowArray = ArrowArrayConverter.ColumnSourceToArray(columnSource, nrows);
      var field = clientTable.Schema.GetFieldByIndex(i);
      var column = new ArrowColumn(field, [arrowArray]);
      columns.Add(column);
    }

    return new ArrowTable(clientTable.Schema, columns);
  }

  public static string Render(ArrowTable table, bool wantHeaders, bool wantLineNumbers) {
    var sw = new StringWriter();
    var numCols = table.ColumnCount;

    var separator = "";

    if (wantHeaders) {
      var headers = table.Schema.FieldsList.Select(f => f.Name);
      if (wantLineNumbers) {
        headers = headers.Prepend("[Row]");
      }

      sw.Write(string.Join('\t', headers));
      separator = "\n";
    }

    var enumerables = Enumerable.Range(0, numCols)
      .Select(i => MakeScalarEnumerable(table.Column(i).Data).GetEnumerator())
      .ToArray();
    var hasMore = new bool[numCols];

    int rowNum = 0;

    var build = new List<object>();

    while (true) {
      for (var i = 0; i != numCols; ++i) {
        hasMore[i] = enumerables[i].MoveNext();
      }

      if (!hasMore.Any(x => x)) {
        break;
      }

      build.Clear();

      if (wantLineNumbers) {
        build.Add($"[{rowNum}]");
      }

      for (var i = 0; i != numCols; ++i) {
        if (!hasMore[i]) {
          build.Add("[exhausted]");
          continue;
        }
        var current = enumerables[i].Current;
        build.Add(current ?? "[null]");
      }

      sw.Write(separator);
      sw.Write(string.Join('\t', build));
      separator = "\n";
      ++rowNum;
    }

    foreach (var e in enumerables) {
      e.Dispose();
    }
    return sw.ToString();
  }

  public static IEnumerable<object> MakeScalarEnumerable(Apache.Arrow.ChunkedArray chunkedArray) {
    var numArrays = chunkedArray.ArrayCount;
    for (var i = 0; i != numArrays; ++i) {
      var array = chunkedArray.ArrowArray(i);
      foreach (var result in (IEnumerable)array) {
        yield return result;
      }
    }
  }
}
