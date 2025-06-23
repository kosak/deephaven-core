using Apache.Arrow.Flight;
using Io.Deephaven.Proto.Backplane.Grpc;
using ArrowColumn = Apache.Arrow.Column;
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
    var arrays = new List<ArrowColumn>();

    for (var i = 0; i != ncols; ++i) {
      var columnSource = clientTable.GetColumn(i);
      var arrowArray = ArrowArrayConverter.ColumnSourceToArray(columnSource, nrows);
      arrays.Add(arrowArray);
    }

    return new ArrowTable(clientTable.Schema, arrays);
  }
}
