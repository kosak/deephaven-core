using System.Collections;
using System.Diagnostics;
using Apache.Arrow;

namespace Deephaven.Dh_NetClient;

public static class TableComparer {
  public static void AssertSame(TableMaker expected, TableHandle actual) {
    var expAsArrow = expected.ToArrowTable();
    var actAsArrow = actual.ToArrowTable();
    AssertSame(expAsArrow, actAsArrow);
  }

  public static void AssertSame(TableMaker expected, IClientTable actual) {
    var expAsArrow = expected.ToArrowTable();
    var actAsArrow = actual.ToArrowTable();
    AssertSame(expAsArrow, actAsArrow);
  }

  public static void AssertSame(Apache.Arrow.Table expected, Apache.Arrow.Table actual) {
    if (expected.ColumnCount != actual.ColumnCount) {
      throw new Exception(
        $"Expected table has {expected.ColumnCount} columns, but actual table has {actual.ColumnCount} columns");
    }

    var numCols = expected.ColumnCount;
    // Collect all type issues (if any) into a single exception
    var issues = new List<string>();
    for (var i = 0; i != numCols; ++i) {
      var exp = expected.Column(i).Field;
      var act = actual.Column(i).Field;

      if (exp.Name != act.Name) {
        throw new Exception($"Column {i}: Expected column name {exp.Name}, actual is {act.Name}");
      }

      if (!ArrowUtil.TypesEqual(exp.DataType, act.DataType)) {
        issues.Add($"Column {i}: Expected column type {exp.DataType}, actual is {act.DataType}");
      }
    }

    if (issues.Count != 0) {
      throw new Exception(string.Join(", ", issues));
    }

    for (var i = 0; i != numCols; ++i) {
      var exp = expected.Column(i);
      var act = actual.Column(i);

      if (exp.Length != act.Length) {
        throw new Exception($"Column {i}: Expected length {exp.Length}, actual length {act.Length}");
      }

      using var expIter = MakeScalarEnumerable(exp.Data).GetEnumerator();
      using var actIter = MakeScalarEnumerable(act.Data).GetEnumerator();

      var rowsConsumed = 0;
      while (true) {
        var expHasMore = expIter.MoveNext();
        var actHasMore = actIter.MoveNext();

        if (expHasMore != actHasMore) {
          throw new Exception(
            $"Iterators have unequal length. After consuming {rowsConsumed} rows, expectedHasMore={expHasMore}, actualHasMore={actHasMore}");
        }

        if (!expHasMore) {
          // Neither iterator has more
          break;
        }

        if (!object.Equals(expIter.Current, actIter.Current)) {
          throw new Exception(
            $"Values differ at row {rowsConsumed}: expected={expIter.Current}, actual={actIter.Current}");
        }
      }
    }
  }

  private static IEnumerable<object> MakeScalarEnumerable(Apache.Arrow.ChunkedArray chunkedArray) {
    var numArrays = chunkedArray.ArrayCount;
    for (var i = 0; i != numArrays; ++i) {
      var array = chunkedArray.ArrowArray(i);
      foreach (var result in (IEnumerable)array) {
        yield return result;
      }
    }
  }
}
