using System;
using Apache.Arrow;
using Deephaven.ManagedClient;

namespace Deephaven.Dh_NetClient;

public static class TableComparer {
  public static void AssertSame(TableMaker expected, TableHandle actual) {
    var expAsArrow = expected.MakeArrowTable();
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

        if (!expIter.Current.Equals(actIter.Current)) {
          throw new Exception(
            $"Values differ at row {rowsConsumed}: expected={expIter.Current}, actual={actIter.Current}");
        }
      }
    }
  }

  private static IEnumerable<object> MakeScalarEnumerable(Apache.Arrow.ChunkedArray chunkedArray) {
    var numArrays = chunkedArray.ArrayCount;
    var arrayVisitor = new MyArrayVisitor();
    for (var i = 0; i != numArrays; ++i) {
      var array = chunkedArray.ArrowArray(i);
      array.Accept(arrayVisitor);
      foreach (var result in arrayVisitor.MakeEnumerable()) {
        yield return result;
      }
    }
  }

  private class MyArrayVisitor : IArrowArrayVisitor {
    public void Visit(IArrowArray array) {
      throw new Exception($"Can't process type {array.Data.DataType}");
    }

    public IEnumerable<object> MakeEnumerable() {
      yield return 12;
    }
  }
}




// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to You under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License.  You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

  static class FieldComparer999 {
    public static bool Compare(Field expected, Field actual) {
      if (ReferenceEquals(expected, actual)) {
        return true;
      }

      if (expected.Name != actual.Name || expected.IsNullable != actual.IsNullable ||
          expected.HasMetadata != actual.HasMetadata) {
        return false;
      }

      if (expected.HasMetadata) {
        if (expected.Metadata.Count != actual.Metadata.Count) {
          return false;
        }

        if (!expected.Metadata.Keys.All(k => actual.Metadata.ContainsKey(k) && expected.Metadata[k] == actual.Metadata[k])) {
          return false;
        }
      }

      var dataTypeComparer = new ArrayDataTypeComparer(expected.DataType);

      actual.DataType.Accept(dataTypeComparer);

      if (!dataTypeComparer.DataTypeMatch) {
        return false;
      }

      return true;
    }
  }
