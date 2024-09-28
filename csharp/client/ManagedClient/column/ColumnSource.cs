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


public interface IInt32ColumnSource : INumericColumnSource<Int32> {
}

public interface IInt64ColumnSource : INumericColumnSource<Int64> {
}

public interface IColumnSourceVisitor {
  void Visit(IInt32ColumnSource cs);
  void Visit(IInt64ColumnSource cs);
}
