using Deephaven.Dh_NetClient;

using DhClientUtility = Deephaven.Dh_NetClient.Utility;

namespace Deephaven.ExcelAddIn.Util;

internal static class Renderer {
  public static object?[,] Render(IClientTable table, bool wantHeaders) {
    var numRows = table.NumRows;
    var numCols = table.NumCols;
    var effectiveNumRows = wantHeaders ? numRows + 1 : numRows;
    var result = new object?[effectiveNumRows, numCols];

    var fields = table.Schema.FieldsList;
    var destStartIndex = 0;
    if (wantHeaders) {
      for (var colIndex = 0; colIndex != numCols; ++colIndex) {
        result[0, colIndex] = fields[colIndex].Name;
      }

      destStartIndex = 1;
    }

    var chunkSize = checked((int)Math.Min(numRows, 16384));

    var endIndex = (UInt64)numRows;

    for (var colIndex = 0; colIndex != numCols; ++colIndex) {
      UInt64 currentIndex = 0;
      var destIndex = destStartIndex;

      var col = table.GetColumn(colIndex);
      var adaptorMaker = new AdaptorMaker(chunkSize);
      col.Accept(adaptorMaker);
      var adaptor = adaptorMaker.Result!;

      while (currentIndex < endIndex) {
        var sizeToCopy = Math.Min(endIndex - currentIndex, (UInt64)chunkSize);
        var rows = RowSequence.CreateSequential(Interval.OfStartAndSize(currentIndex, sizeToCopy));
        var destData = adaptor.FillChunk(col, rows);
        currentIndex += sizeToCopy;

        for (UInt64 i = 0; i != sizeToCopy; ++i) {
          result[destIndex++, colIndex] = destData[i];
        }
      }
    }

    return result;
  }

  public static object?[,] RenderHeaders(IClientTable table) {
    var numCols = table.NumCols;
    var result = new object?[1, numCols];

    var fields = table.Schema.FieldsList;
    for (var colIndex = 0; colIndex != numCols; ++colIndex) {
      result[0, colIndex] = fields[colIndex].Name;
    }
    return result;
  }

  private class AdaptorMaker(int size) :
    IColumnSourceVisitor<ICharColumnSource>,
    IColumnSourceVisitor<IByteColumnSource>,
    IColumnSourceVisitor<IInt16ColumnSource>,
    IColumnSourceVisitor<IInt32ColumnSource>,
    IColumnSourceVisitor<IInt64ColumnSource>,
    IColumnSourceVisitor<IFloatColumnSource>,
    IColumnSourceVisitor<IDoubleColumnSource>,
    IColumnSourceVisitor<IBooleanColumnSource>,
    IColumnSourceVisitor<IStringColumnSource>,
    IColumnSourceVisitor<IDateTimeOffsetColumnSource>,
    IColumnSourceVisitor<IDateOnlyColumnSource>,
    IColumnSourceVisitor<ITimeOnlyColumnSource> {
    public IAdaptor? Result { get; private set; }

    public void Visit(IColumnSource cs) {
      throw new NotImplementedException($"Don't have an adaptor for type {DhClientUtility.FriendlyTypeName(cs.GetType())}");
    }

    public void Visit(ICharColumnSource _) => Result = new Adaptor<char>(size, ch => ch.ToString());
    public void Visit(IByteColumnSource _) => Result = new Adaptor<sbyte>(size, x => x);
    public void Visit(IInt16ColumnSource _) => Result = new Adaptor<Int16>(size, x => x);
    public void Visit(IInt32ColumnSource _) => Result = new Adaptor<Int32>(size, x => x);
    public void Visit(IInt64ColumnSource _) => Result = new Adaptor<Int64>(size, x => x);
    public void Visit(IFloatColumnSource _) => Result = new Adaptor<float>(size, x => x);
    public void Visit(IDoubleColumnSource _) => Result = new Adaptor<double>(size, x => x);
    public void Visit(IBooleanColumnSource _) => Result = new Adaptor<bool>(size, x => x);
    public void Visit(IStringColumnSource _) => Result = new Adaptor<string>(size, x => x);
    public void Visit(IDateTimeOffsetColumnSource _) => Result = new Adaptor<DateTimeOffset>(size,
      dt => dt.ToString("o", null));
    public void Visit(IDateOnlyColumnSource _) => Result = new Adaptor<DateOnly>(size,
        dt => dt.ToString("o", null));
    public void Visit(ITimeOnlyColumnSource _) => Result = new Adaptor<TimeOnly>(size,
      dt => dt.ToString("o", null));
  }

  private interface IAdaptor {
    public object[] FillChunk(IColumnSource cols, RowSequence rows);
  }

  private sealed class Adaptor<T> : IAdaptor {
    private readonly Func<T, object> _converter;
    private readonly Chunk<T> _intermediateChunk;
    private readonly Chunk<bool> _nulls;
    private readonly object[] _destData;

    public Adaptor(int size, Func<T, object> converter) {
      _converter = converter;
      _intermediateChunk = Chunk<T>.Create(size);
      _nulls = Chunk<bool>.Create(size);
      _destData = new object[size];
    }

    public object[] FillChunk(IColumnSource col, RowSequence rows) {
      col.FillChunk(rows, _intermediateChunk, _nulls);
      for (var i = 0; i != _intermediateChunk.Size; ++i) {
        // Assume null, which we render as empty string.
        object destValue = "";
        if (!_nulls.Data[i]) {
          var srcValue = _intermediateChunk.Data[i];
          destValue = _converter(srcValue);
        }

        _destData[i] = destValue;
      }
      return _destData;
    }
  }
}
