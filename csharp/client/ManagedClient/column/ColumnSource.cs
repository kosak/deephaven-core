global using IInt64ColumnSource = Deephaven.ManagedClient.INumericColumnSource<System.Int64>;

namespace Deephaven.ManagedClient;

public interface IColumnSource {
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
