//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
using System.Collections;
using Apache.Arrow;
using Apache.Arrow.Flight;
using Io.Deephaven.Proto.Backplane.Grpc;
using Array = System.Array;
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
    var dtc = new ArrowDataTypeComparer(lhs);
    rhs.Accept(dtc);
    return dtc.Result;
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



  // THIS THING takes an arrow chunkedarray and returns an enumerable of all of its elements, boxed as an object, end of story.
  // Give me a chunked array of underlying iarrowarrays of int32, and I will give you an enumerable of boxed int32 liek [3, 4, 17]
  // COMPLICATION it works with lists
  // Give me a chunked array of underlying iarrowarrays of list<int32>, and I will give you an enumerable of List<int32> like [[3,4], [82,5,8], [2, 9, 13, 14]]

  // this should probably have a name which indicates just how narrow and limited it is
  public static IEnumerable<object> MakeScalarEnumerable(Apache.Arrow.ChunkedArray chunkedArray) {
    var numArrays = chunkedArray.ArrayCount;
    var visitor = new ScalarEnumerableVisitor();
    for (var i = 0; i != numArrays; ++i) {
      var array = chunkedArray.ArrowArray(i);
      array.Accept(visitor);
      foreach (var result in visitor.Result) {
        yield return result;
      }
    }
  }

  public static IEnumerable<object> MakeScalarEnumerableSIMPLE(Apache.Arrow.ChunkedArray chunkedArray) {
    var numArrays = chunkedArray.ArrayCount;
    for (var i = 0; i != numArrays; ++i) {
      var array = chunkedArray.ArrowArray(i);
      foreach (var result in (IEnumerable)array) {
        yield return result;
      }
    }
  }

  // IF CHUNKED ARRAYS DID NOT EXIST
  public static IEnumerable MakeScalarEnumerableFROMIDATAWHATEVER(Apache.Arrow.IArrowArray array) {
    // Int32Array will implement IEnumerable and IEnumerable<int32>

    return (IEnumerable)array;
  }



  private class ScalarEnumerableVisitor : Apache.Arrow.IArrowArrayVisitor,
    IArrowArrayVisitor<ListArray> {
    public IEnumerable Result = Array.Empty<object>();

    public void Visit(IArrowArray array) {
      Result = (IEnumerable)array;
    }

    public void Visit(ListArray array) {
      Result = ListArrayHelper(array);
    }

    private IEnumerable ListArrayHelper(ListArray array) {
      var innerVisitor = new ScalarEnumerableVisitor();
      for (var i = 0; i != array.Length; ++i) {
        var slice = array.GetSlicedValues(i);
        slice.Accept(innerVisitor);
        yield return new ObjectListWithEqualityAndToString(innerVisitor.Result);
      }
    }
  }

  private sealed class ObjectListWithEqualityAndToString : IEquatable<ObjectListWithEqualityAndToString> {
    private readonly object?[] _values;

    public ObjectListWithEqualityAndToString(IEnumerable items) {
      _values = items.Cast<object?>().ToArray();
    }

    public bool Equals(ObjectListWithEqualityAndToString? other) {
      return other != null &&
        StructuralComparisons.StructuralEqualityComparer.Equals(_values, other._values);
    }

    public override bool Equals(object? other) {
      return Equals(other as ObjectListWithEqualityAndToString);
    }

    public override int GetHashCode() {
      return StructuralComparisons.StructuralEqualityComparer.GetHashCode(_values);
    }

    public override string ToString() {
      var filterNull = _values.Select(e => e ?? "[null]");
      return $"[{string.Join(", ", filterNull)}]";
    }
  }
}
