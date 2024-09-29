namespace Deephaven.ManagedClient;

public interface IColumnSource {
  void FillChunk(RowSequence rows, Chunk dest, BooleanChunk? nullFlags);
  void AcceptVisitor(IColumnSourceVisitor visitor);
}

public interface IMutableColumnSource : IColumnSource {

}

public interface INumericColumnSource<T> : IColumnSource where T : struct {

}

public interface IGenericColumnSource<T> : IColumnSource {

}

public interface IMutableNumericColumnSource<T> : INumericColumnSource<T>, IMutableColumnSource where T : struct {
}

public interface IMutableGenericColumnSource<T> : IGenericColumnSource<T>, IMutableColumnSource {
}

public interface ICharColumnSource : INumericColumnSource<char> {
}

public interface IInt16ColumnSource : INumericColumnSource<Int16> {
}

public interface IInt32ColumnSource : INumericColumnSource<Int32> {
}

public interface IInt64ColumnSource : INumericColumnSource<Int64> {
}

public interface IFloatColumnSource : INumericColumnSource<float> {
}

public interface IDoubleColumnSource : INumericColumnSource<double> {
}

public interface ITimestampColumnSource : IGenericColumnSource<DhDateTime> {
}

public interface ILocalDateColumnSource : IGenericColumnSource<LocalDate> {
}

public interface ILocalTimeColumnSource : IGenericColumnSource<LocalTime> {
}

public interface IColumnSourceVisitor {
  void Visit(ICharColumnSource cs);
  void Visit(IInt16ColumnSource cs);
  void Visit(IInt32ColumnSource cs);
  void Visit(IInt64ColumnSource cs);
  void Visit(IFloatColumnSource cs);
  void Visit(IDoubleColumnSource cs);
  void Visit(ITimestampColumnSource cs);
  void Visit(ILocalDateColumnSource cs);
  void Visit(ILocalTimeColumnSource cs);
}
