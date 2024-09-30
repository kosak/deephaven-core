using Apache.Arrow;
using Apache.Arrow.Flight;
using Apache.Arrow.Types;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.ManagedClient;

public static class ArrowUtil {
  public static FlightDescriptor ConvertTicketToFlightDescriptor(Ticket ticket) {
    var bytes = ticket.Ticket_.Span;
    if (bytes.Length != 5 || bytes[0] != 'e') {
      throw new Exception("Ticket is not in correct format for export");
    }

    var value = BitConverter.ToUInt32(bytes.Slice(1));
    return FlightDescriptor.CreatePathDescriptor("export", value.ToString());
  }

  public static IColumnSource MakeColumnSource(Column column) {
    var visitor = new MyVisitor(column.Data);
    column.Type.Accept(visitor);
    if (visitor.Result == null) {
      throw new Exception($"No result set for {column.Data.DataType}");
    }
    return visitor.Result;
  }

  public static (IColumnSource, int) MakeColumnSourceFromListArray(ListArray la) {
    if (la.Length != 1) {
      throw new Exception($"Expected ListArray of length 1, got {la.Length}");
    }
    var array = la.GetSlicedValues(0);
    var chunkedArray = new ChunkedArray(new[]{array});

    var visitor = new MyVisitor(chunkedArray);
    array.Data.DataType.Accept(visitor);
    return (visitor.Result!, array.Length);
  }


  private class MyVisitor(ChunkedArray chunkedArray) :
    IArrowTypeVisitor<UInt16Type>,
    IArrowTypeVisitor<Int8Type>,
    IArrowTypeVisitor<Int16Type>,
    IArrowTypeVisitor<Int32Type>,
    IArrowTypeVisitor<Int64Type>,
    IArrowTypeVisitor<FloatType>,
    IArrowTypeVisitor<DoubleType>,
    IArrowTypeVisitor<BooleanType>,
    IArrowTypeVisitor<StringType>,
    IArrowTypeVisitor<TimestampType>,
    IArrowTypeVisitor<Date64Type>,
    IArrowTypeVisitor<Time64Type> {
    public IColumnSource? Result { get; private set; }

    public void Visit(UInt16Type type) {
      Result = new CharArrowColumnSource(chunkedArray);
    }

    public void Visit(Int8Type type) {
      Result = new ByteArrowColumnSource(chunkedArray);
    }

    public void Visit(Int16Type type) {
      Result = new Int16ArrowColumnSource(chunkedArray);
    }

    public void Visit(Int32Type type) {
      Result = new Int32ArrowColumnSource(chunkedArray);
    }

    public void Visit(Int64Type type) {
      Result = new Int64ArrowColumnSource(chunkedArray);
    }

    public void Visit(FloatType type) {
      Result = new FloatArrowColumnSource(chunkedArray);
    }

    public void Visit(DoubleType type) {
      Result = new DoubleArrowColumnSource(chunkedArray);
    }

    public void Visit(BooleanType type) {
      Result = new BooleanArrowColumnSource(chunkedArray);
    }

    public void Visit(StringType type) {
      Result = new StringArrowColumnSource(chunkedArray);
    }

    public void Visit(TimestampType type) {
      Result = new TimestampArrowColumnSource(chunkedArray);
    }

    public void Visit(Date64Type type) {
      Result = new LocalDateArrowColumnSource(chunkedArray);
    }

    public void Visit(Time64Type type) {
      Result = new LocalTimeArrowColumnSource(chunkedArray);
    }

    public void Visit(IArrowType type) {
      throw new Exception($"type {type.Name} is not supported");
    }
  }
}
