namespace Deephaven.ManagedClient;

public class TableMaker {
  private readonly List<ColumnInfo> _columnInfos = new();

  public void AddColumn(string name, IEnumerable<byte> values) {
    AddColumn(name, values.Select(v => (byte?)v));
  }

  public void AddColumn(string name, IEnumerable<char> values) {
    AddColumn(name, values.Select(v => (char?)v));
  }

  public void AddColumn(string name, IEnumerable<sbyte> values) {
    AddColumn(name, values.Select(v => (sbyte?)v));
  }

  public void AddColumn(string name, IEnumerable<Int16> values) {
    AddColumn(name, values.Select(v => (Int16?)v));
  }

  public void AddColumn(string name, IEnumerable<Int32> values) {
    AddColumn(name, values.Select(v => (Int32?)v));
  }

  public void AddColumn(string name, IEnumerable<Int64> values) {
    AddColumn(name, values.Select(v => (Int64?)v));
  }

  public void AddColumn(string name, IEnumerable<float> values) {
    AddColumn(name, values.Select(v => (float?)v));
  }

  public void AddColumn(string name, IEnumerable<float> values) {
    AddColumn(name, values.Select(v => (float?)v));
  }



  public void AddColumn(string name, IEnumerable<byte?> values) {
    AddNullableColumn(name, values, new Apache.Arrow.UInt8Array.Builder());
  }

  public void AddColumn(string name, IEnumerable<char?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? (UInt16)0, v.HasValue));
    var builder = new Apache.Arrow.UInt16Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<sbyte> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Int8Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int16> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Int16Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int32> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Int32Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int64> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Int64Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<float> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.FloatArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<double> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.DoubleArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<DateTimeOffset> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.TimestampArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<TimeOnly> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Time32Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<DateOnly> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    var builder = new Apache.Arrow.Date32Array.Builder();
    AddColumnHelper<DateOnly, Apache.Arrow.Date32Array, Apache.Arrow.Date32Array.Builder>(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<string?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? "", v != null));
    var builder = new Apache.Arrow.StringArray.Builder();
    AddColumnHelper<string, Apache.Arrow.StringArray, Apache.Arrow.StringArray.Builder>(name, wrapped, builder);
  }

  private void AddSimpleColumn<T, TArray, TBuilder>(string name, IEnumerable<T> values,
    Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> builder) where T : struct
    where TArray : Apache.Arrow.IArrowArray
    where TBuilder : Apache.Arrow.IArrowArrayBuilder<TArray> {
    var wrapped = values.Select(v => KeyValuePair.Create(v, true));
    AddColumnHelper(name, wrapped, builder);
  }

  private void AddNullableColumn<T, TArray, TBuilder>(string name, IEnumerable<T?> values,
    Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> builder) where T : struct
    where TArray : Apache.Arrow.IArrowArray
    where TBuilder : Apache.Arrow.IArrowArrayBuilder<TArray> {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? default, v.HasValue));
    AddColumnHelper(name, wrapped, builder);
  }

  // IArrowArrayBuilder<byte, TArray, TBuilder>
  private void AddColumnHelper<T, TArray, TBuilder>(string name,
    IEnumerable<KeyValuePair<T, bool>> kvps,
    Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> builder)
    where TArray : Apache.Arrow.IArrowArray
    where TBuilder : Apache.Arrow.IArrowArrayBuilder<TArray> {
    foreach (var kvp in kvps) {
      if (kvp.Value) {
        builder.Append(kvp.Key);
      } else {
        builder.AppendNull();
      }
    }
    var array = builder.Build(null);

    // var (typeName, componentTypeName) = cb.GetDeephavenMetadata();

    // var kvMetadata = new KeyValueMetadata();
    // OkOrThrow(DEEPHAVEN_LOCATION_EXPR(
    //   kv_metadata->Set(DeephavenMetadataConstants::Keys::Type(), std::move(deephaven_metadata_type_name))));
    // if (deephaven_metadata_component_type_name.has_value()) {
    //   OkOrThrow(DEEPHAVEN_LOCATION_EXPR(
    //     kv_metadata->Set(DeephavenMetadataConstants::Keys::ComponentType(),
    //       std::move(*deephaven_metadata_component_type_name))));
    // }

    _columnInfos.Add(new ColumnInfo(name, array));
  }

  private record ColumnInfo(string Name, Apache.Arrow.IArrowArray Data);
}
