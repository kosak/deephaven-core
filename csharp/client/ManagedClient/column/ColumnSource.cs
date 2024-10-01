global using ICharColumnSource = Deephaven.ManagedClient.IColumnSource<char>;
global using IByteColumnSource = Deephaven.ManagedClient.IColumnSource<sbyte>;
global using IInt16ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int16>;
global using IInt32ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int32>;
global using IInt64ColumnSource = Deephaven.ManagedClient.IColumnSource<System.Int64>;
global using IFloatColumnSource = Deephaven.ManagedClient.IColumnSource<float>;
global using IDoubleColumnSource = Deephaven.ManagedClient.IColumnSource<double>;
global using IBooleanColumnSource = Deephaven.ManagedClient.IColumnSource<bool>;
global using IStringColumnSource = Deephaven.ManagedClient.IColumnSource<string>;
global using ITimestampColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.DhDateTime>;
global using ILocalDateColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.LocalDate>;
global using ILocalTimeColumnSource = Deephaven.ManagedClient.IColumnSource<Deephaven.ManagedClient.LocalTime>;

namespace Deephaven.ManagedClient;

public interface IColumnSource {
  void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  void Accept(IColumnSourceVisitor visitor);
}

public interface IColumnSource<T> : IColumnSource {

}

public interface IColumnSourceVisitor {
  void Visit(IColumnSource cs);
}

public interface IColumnSourceVisitor<in T> where T : IColumnSourceVisitor {
  void Visit(T cs);
}
