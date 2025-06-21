using Apache.Arrow;

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

  public void AddColumn(string name, IEnumerable<double> values) {
    AddColumn(name, values.Select(v => (double?)v));
  }

  public void AddColumn(string name, IEnumerable<DateTimeOffset> values) {
    AddColumn(name, values.Select(v => (DateTimeOffset?)v));
  }

  public void AddColumn(string name, IEnumerable<TimeOnly> values) {
    AddColumn(name, values.Select(v => (TimeOnly?)v));
  }

  public void AddColumn(string name, IEnumerable<DateOnly> values) {
    AddColumn(name, values.Select(v => (DateOnly?)v));
  }

  public void AddColumn(string name, IEnumerable<byte?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.UInt8Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<char?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? (UInt16)0, v.HasValue));
    var builder = new Apache.Arrow.UInt16Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<sbyte?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.Int8Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int16?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.Int16Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int32?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.Int32Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<Int64?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.Int64Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<float?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.FloatArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<double?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? 0, v.HasValue));
    var builder = new Apache.Arrow.DoubleArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<DateTimeOffset?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? new DateTimeOffset(), v.HasValue));
    var builder = new Apache.Arrow.TimestampArray.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<TimeOnly?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? new TimeOnly(), v.HasValue));
    var builder = new Apache.Arrow.Time32Array.Builder();
    AddColumnHelper(name, wrapped, builder);
  }

  public void AddColumn(string name, IEnumerable<DateOnly?> values) {
    var wrapped = values.Select(v => KeyValuePair.Create(v ?? new DateOnly(), v.HasValue));
    var builder = new Apache.Arrow.Date32Array.Builder();
    AddColumnHelper<DateOnly, Apache.Arrow.Date32Array, Apache.Arrow.Date32Array.Builder>(name, wrapped, builder);
  }

  public void AddColumn<T>(string name, IEnumerable<List<T>?> values) {
    // needs a different approach
    throw new Exception("TODO(kosak)");
  }

  public void AddColumnSoSayWeAll<T>(string name, IEnumerable<T> values) {
    var cb = ColumnBuilder.ForType<T>();
    foreach (var value in values) {
      cb.Append(value);
    }
    var array = cb.Build();
    cb.GetDeephavenMetadata(out var typeName, out var componentTypeName);

    var kvMetadata = new List<KeyValuePair<string, string>>();
    kvMetadata.Add(KeyValuePair.Create(DeephavenMetadataConstants.Keys.Type, typeName));
    if (componentTypeName != null) {
      kvMetadata.Add(KeyValuePair.Create(DeephavenMetadataConstants.Keys.ComponentType, componentTypeName));
    }
    _columnInfos.Add(new ColumnInfo(name, array, kvMetadata.ToArray()));
  }


  public void AddColumn(string name, IEnumerable<string?> values) {
    // Arrow StringArray.Builder is special.
    var builder = new Apache.Arrow.StringArray.Builder();
    foreach (var value in values) {
      if (value != null) {
        builder.Append(value);
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

#if false
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
#endif

#if false
  public TableHandle MakeTable(TableHandleManager manager) {
    auto schema = MakeSchema();

    auto wrapper = manager.CreateFlightWrapper();
    auto ticket = manager.NewTicket();
    auto flight_descriptor = ArrowUtil::ConvertTicketToFlightDescriptor(ticket);

    arrow::flight::FlightCallOptions options;
    wrapper.AddHeaders(&options);

    auto res = wrapper.FlightClient()->DoPut(options, flight_descriptor, schema);
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res));
    auto data = GetColumnsNotEmpty();
    auto num_rows = data.back()->length();
    auto batch = arrow::RecordBatch::Make(schema, num_rows, std::move(data));

    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->WriteRecordBatch(*batch)));
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->DoneWriting()));

    std::shared_ptr<arrow::Buffer> buf;
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->reader->ReadMetadata(&buf)));
    OkOrThrow(DEEPHAVEN_LOCATION_EXPR(res->writer->Close()));
    return manager.MakeTableHandleFromTicket(std::move(ticket));
  }
#endif

  private Apache.Arrow.Schema MakeSchema() {
    ValidateSchema();

    var sb = new Apache.Arrow.Schema.Builder();
    foreach (var ci in _columnInfos) {
      var arrowType = ci.Data.Data.DataType;
      var field = new Apache.Arrow.Field(ci.Name, arrowType, true, ci.ArrowMetadata);
      sb.Field(field);
    }

    return sb.Build();
  }

  private void ValidateSchema() {
    if (_columnInfos.Count == 0) {
      return;
    }

    var numRows = _columnInfos[0].Data.Length;
    for (var i = 1; i != _columnInfos.Count; ++i) {
      var ci = _columnInfos[i];
      if (ci.Data.Length != numRows) {
        var message =
          $"Column sizes not consistent: column 0 has size {numRows}, but column {i} has size {ci.Data.Length}";
        throw new Exception(message);
      }
    }
  }

  private class ColumnBuilder {
    public static ColumnBuilder<T> ForType<T>() {
      var type = typeof(T);
      if (type == typeof(Int32)) {
        var arrowBuilder = new Apache.Arrow.Int32Array.Builder();
        ColumnBuilder builder = new TypicalBuilder<Int32, Apache.Arrow.Int32Array, Apache.Arrow.Int32Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Int32);
        return (ColumnBuilder<T>)builder;
      }
    }
  }

  private abstract class ColumnBuilder<T> : ColumnBuilder {
    public abstract void Append(T item);
    public abstract void AppendNull();

    public abstract Apache.Arrow.IArrowArray Build();

    public abstract (string, string?) GetDeephavenMetadata();
  }

  private sealed class TypicalBuilder<T, TArray, TBuilder> : ColumnBuilder<T>
    where TArray : Apache.Arrow.IArrowArray
    where TBuilder : Apache.Arrow.IArrowArrayBuilder<TArray> {
    private readonly Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> _builder;
    private readonly string _typeName;

    public TypicalBuilder(IArrowArrayBuilder<T, TArray, TBuilder> builder, string typeName) {
      _builder = builder;
      _typeName = typeName;
    }

    public override void Append(T item) {
      _builder.Append(item);
    }

    public override void AppendNull() {
      _builder.AppendNull();
    }

    public override (string, string?) GetDeephavenMetadata() {
      return (_typeName, null);
    }

    public override IArrowArray Build() {
      return _builder.Build(null);
    }
  }

  private record ColumnInfo(string Name,
    Apache.Arrow.IArrowArray Data,
    KeyValuePair<string, string>[] ArrowMetadata);
}
