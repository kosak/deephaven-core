global using ICharColumnSource = Deephaven.ManagedClient.IGenericColumnSource<char>;
global using IByteColumnSource = Deephaven.ManagedClient.IGenericColumnSource<sbyte>;
global using IInt16ColumnSource = Deephaven.ManagedClient.IGenericColumnSource<System.Int16>;
global using IInt32ColumnSource = Deephaven.ManagedClient.IGenericColumnSource<System.Int32>;
global using IInt64ColumnSource = Deephaven.ManagedClient.IGenericColumnSource<System.Int64>;
global using IFloatColumnSource = Deephaven.ManagedClient.IGenericColumnSource<float>;
global using IDoubleColumnSource = Deephaven.ManagedClient.IGenericColumnSource<double>;
global using IBooleanColumnSource = Deephaven.ManagedClient.IGenericColumnSource<bool>;
global using IStringColumnSource = Deephaven.ManagedClient.IGenericColumnSource<string>;
global using ITimestampColumnSource = Deephaven.ManagedClient.IGenericColumnSource<Deephaven.ManagedClient.DhDateTime>;
global using ILocalDateColumnSource = Deephaven.ManagedClient.IGenericColumnSource<Deephaven.ManagedClient.LocalDate>;
global using ILocalTimeColumnSource = Deephaven.ManagedClient.IGenericColumnSource<Deephaven.ManagedClient.LocalTime>;

namespace Deephaven.ManagedClient;

public interface IColumnSource {
  void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  void AcceptVisitor(IColumnSourceVisitor visitor);
}

public interface IGenericColumnSource<T> : IColumnSource {

}

public interface IColumnSourceVisitor {
  void Visit(ICharColumnSource cs);
  void Visit(IByteColumnSource cs);
  void Visit(IInt16ColumnSource cs);
  void Visit(IInt32ColumnSource cs);
  void Visit(IInt64ColumnSource cs);
  void Visit(IFloatColumnSource cs);
  void Visit(IDoubleColumnSource cs);
  void Visit(IBooleanColumnSource cs);
  void Visit(IStringColumnSource cs);
  void Visit(ITimestampColumnSource cs);
  void Visit(ILocalDateColumnSource cs);
  void Visit(ILocalTimeColumnSource cs);
}
