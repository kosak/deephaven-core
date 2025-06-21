using Apache.Arrow.Flight;
using Grpc.Core;

namespace Deephaven.ManagedClient;

public class TableMaker {
  private readonly List<ColumnInfo> _columnInfos = new();

  public void AddColumn<T>(string name, IEnumerable<T> values) {
    var cb = ColumnBuilder.ForType<T>();
    foreach (var value in values) {
      cb.Append(value);
    }
    var array = cb.Build();
    var (typeName, componentTypeName) = cb.GetDeephavenMetadata();

    var kvMetadata = new List<KeyValuePair<string, string>>();
    kvMetadata.Add(KeyValuePair.Create(DeephavenMetadataConstants.Keys.Type, typeName));
    if (componentTypeName != null) {
      kvMetadata.Add(KeyValuePair.Create(DeephavenMetadataConstants.Keys.ComponentType, componentTypeName));
    }
    _columnInfos.Add(new ColumnInfo(name, array, kvMetadata.ToArray()));
  }

  public TableHandle MakeTable(TableHandleManager manager) {
    var schema = MakeSchema();

    var server = manager.Server!;

    var ticket = server.NewTicket();
    var flightDescriptor = ArrowUtil.ConvertTicketToFlightDescriptor(ticket);

    var headers = new Metadata();
    server.ForEachHeaderNameAndValue(headers.Add);

    var res = server.FlightClient.StartPut(flightDescriptor, schema, headers).Result;
    var data = GetColumnsNotEmpty();
    var numRows = data[^1].Length;

    var recordBatch = new Apache.Arrow.RecordBatch(schema, data, numRows);

    res.RequestStream.WriteAsync(recordBatch).Wait();
    res.RequestStream.CompleteAsync().Wait();

    while (res.ResponseStream.MoveNext().Result) {
      // eat values. Is this necessary?
    }

    res.Dispose();
    return manager.MakeTableHandleFromTicket(ticket);
  }

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

  private Apache.Arrow.IArrowArray[] GetColumnsNotEmpty() {
    var result = _columnInfos.Select(ci => ci.Data).ToArray();
    if (result.Length == 0) {
      throw new Exception("Can't make table with no columns");
    }
    return result;
  }

  private class ColumnBuilder {
    public static ColumnBuilder<T> ForType<T>() {
      return (ColumnBuilder<T>)ForType(typeof(T));
    }

    public static ColumnBuilder ForType(Type type) {
      var underlyingType = Nullable.GetUnderlyingType(type);
      if (underlyingType != null) {
        var miGeneric = typeof(ColumnBuilder).GetMethod(nameof(ForNullableType));
        if (miGeneric == null) {
          throw new Exception($"Can't find {nameof(ForNullableType)}");
        }
        var miInstantiated = miGeneric.MakeGenericMethod(underlyingType);
        return (ColumnBuilder)miInstantiated.Invoke(null, null)!;
      }


      if (type == typeof(sbyte)) {
        var arrowBuilder = new Apache.Arrow.Int8Array.Builder();
        return new TypicalBuilder<sbyte, Apache.Arrow.Int8Array, Apache.Arrow.Int8Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Int8);
      }

      if (type == typeof(Int16)) {
        var arrowBuilder = new Apache.Arrow.Int16Array.Builder();
        return new TypicalBuilder<Int16, Apache.Arrow.Int16Array, Apache.Arrow.Int16Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Int16);
      }

      if (type == typeof(Int32)) {
        var arrowBuilder = new Apache.Arrow.Int32Array.Builder();
        return new TypicalBuilder<Int32, Apache.Arrow.Int32Array, Apache.Arrow.Int32Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Int32);
      }

      if (type == typeof(Int64)) {
        var arrowBuilder = new Apache.Arrow.Int64Array.Builder();
        return new TypicalBuilder<Int64, Apache.Arrow.Int64Array, Apache.Arrow.Int64Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Int64);
      }

      if (type == typeof(float)) {
        var arrowBuilder = new Apache.Arrow.FloatArray.Builder();
        return new TypicalBuilder<float, Apache.Arrow.FloatArray, Apache.Arrow.FloatArray.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Float);
      }

      if (type == typeof(double)) {
        var arrowBuilder = new Apache.Arrow.DoubleArray.Builder();
        return new TypicalBuilder<double, Apache.Arrow.DoubleArray, Apache.Arrow.DoubleArray.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Double);
      }

      if (type == typeof(bool)) {
        var arrowBuilder = new Apache.Arrow.BooleanArray.Builder();
        return new TypicalBuilder<bool, Apache.Arrow.BooleanArray, Apache.Arrow.BooleanArray.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.Bool);
      }

      if (type == typeof(char)) {
        return new CharColumnBuilder();
      }

      if (type == typeof(string)) {
        return new StringColumnBuilder();
      }

      if (type == typeof(DateTimeOffset)) {
        var arrowBuilder = new Apache.Arrow.TimestampArray.Builder();
        return new TypicalBuilder<DateTimeOffset, Apache.Arrow.TimestampArray, Apache.Arrow.TimestampArray.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.DateTime);
      }

      if (type == typeof(DateOnly)) {
        var arrowBuilder = new Apache.Arrow.Date64Array.Builder();
        return new TypicalBuilder<DateOnly, Apache.Arrow.Date64Array, Apache.Arrow.Date64Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.LocalDate);
      }

      if (type == typeof(TimeOnly)) {
        var arrowBuilder = new Apache.Arrow.Time64Array.Builder();
        return new TypicalBuilder<TimeOnly, Apache.Arrow.Time64Array, Apache.Arrow.Time64Array.Builder>(
          arrowBuilder, DeephavenMetadataConstants.Types.LocalTime);
      }

      throw new Exception($"ColumnBuilder does not support type {Utility.FriendlyTypeName(type)}");
    }
    
    public static ColumnBuilder<T?> ForNullableType<T>() where T : struct {
      var underlyingCb = ForType<T>();
      return new NullableBuilder<T>(underlyingCb);
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

    public TypicalBuilder(Apache.Arrow.IArrowArrayBuilder<T, TArray, TBuilder> builder, string typeName) {
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

    public override Apache.Arrow.IArrowArray Build() {
      return _builder.Build(null);
    }
  }

  private sealed class CharColumnBuilder : ColumnBuilder<char> {
    private readonly Apache.Arrow.UInt16Array.Builder _builder = new();

    public override void Append(char item) {
      _builder.Append(item);
    }

    public override void AppendNull() {
      _builder.AppendNull();
    }

    public override (string, string?) GetDeephavenMetadata() {
      return (DeephavenMetadataConstants.Types.Char16, null);
    }

    public override Apache.Arrow.IArrowArray Build() {
      return _builder.Build();
    }
  }

  private sealed class StringColumnBuilder : ColumnBuilder<string> {
    private readonly Apache.Arrow.StringArray.Builder _builder = new();

    public override void Append(string item) {
      _builder.Append(item);
    }

    public override void AppendNull() {
      _builder.AppendNull();
    }

    public override (string, string?) GetDeephavenMetadata() {
      return (DeephavenMetadataConstants.Types.String, null);
    }

    public override Apache.Arrow.IArrowArray Build() {
      return _builder.Build();
    }
  }

  private sealed class NullableBuilder<T> : ColumnBuilder<T?> where T : struct {
    private readonly ColumnBuilder<T> _underlyingBuilder;

    public NullableBuilder(ColumnBuilder<T> underlyingBuilder) {
      _underlyingBuilder = underlyingBuilder;
    }

    public override void Append(T? item) {
      if (item.HasValue) {
        _underlyingBuilder.Append(item.Value);
      } else {
        _underlyingBuilder.AppendNull();
      }
    }

    public override void AppendNull() {
      _underlyingBuilder.AppendNull();
    }

    public override Apache.Arrow.IArrowArray Build() {
      return _underlyingBuilder.Build();
    }

    public override (string, string?) GetDeephavenMetadata() {
      return _underlyingBuilder.GetDeephavenMetadata();
    }
  }

  private record ColumnInfo(string Name,
    Apache.Arrow.IArrowArray Data,
    KeyValuePair<string, string>[] ArrowMetadata);
}
