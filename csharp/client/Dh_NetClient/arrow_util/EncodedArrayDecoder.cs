//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
using Apache.Arrow;
using Apache.Arrow.Types;

namespace Deephaven.Dh_NetClient;

/// <summary>
/// Decodes Arrow arrays that arrive with a transport-level encoding — dictionary-encoded,
/// run-end-encoded, or run-end-encoded over a dictionary — into plain arrays of the underlying
/// value type. The Deephaven server sends such encodings when a table carries a BarrageSchema
/// attribute; the client's contract is to hand the caller plain data.
/// </summary>
public static class EncodedArrayDecoder {
  public static bool IsEncodedType(IArrowType type) {
    return type is DictionaryType or RunEndEncodedType;
  }

  /// <summary>
  /// Strips encodings from a type: Dictionary&lt;index, T&gt; and RunEndEncoded&lt;T&gt;
  /// (in any nesting) map to T; all other types map to themselves.
  /// </summary>
  public static IArrowType DecodeType(IArrowType type) {
    while (true) {
      switch (type) {
        case DictionaryType dt:
          type = dt.ValueType;
          break;
        case RunEndEncodedType ret:
          type = ret.ValuesDataType;
          break;
        default:
          return type;
      }
    }
  }

  public static Field DecodeField(Field field) {
    var decodedType = DecodeType(field.DataType);
    if (ReferenceEquals(decodedType, field.DataType)) {
      return field;
    }
    return new Field(field.Name, decodedType, field.IsNullable, field.Metadata);
  }

  public static Schema DecodeSchema(Schema schema) {
    if (!schema.FieldsList.Any(f => IsEncodedType(f.DataType))) {
      return schema;
    }
    return new Schema(schema.FieldsList.Select(DecodeField), schema.Metadata);
  }

  /// <summary>
  /// Returns a record batch whose encoded columns have been decoded to plain arrays,
  /// with the schema adjusted to match. Batches with no encoded columns are returned as-is.
  /// </summary>
  public static RecordBatch DecodeRecordBatch(RecordBatch batch) {
    var decodedSchema = DecodeSchema(batch.Schema);
    if (ReferenceEquals(decodedSchema, batch.Schema)) {
      return batch;
    }
    var decodedColumns = batch.Arrays.Select(Decode);
    return new RecordBatch(decodedSchema, decodedColumns, batch.Length);
  }

  /// <summary>
  /// Returns a plain array with the same logical contents as 'array'.
  /// Arrays that are not encoded are returned as-is.
  /// </summary>
  public static IArrowArray Decode(IArrowArray array) {
    switch (array) {
      case DictionaryArray da:
        return DecodeDictionary(da);
      case RunEndEncodedArray rea:
        return DecodeRunEnds(rea);
      default:
        return array;
    }
  }

  /// <summary>
  /// Expands a DictionaryArray (indices + dictionary values) into a plain array of the
  /// dictionary's value type. The dictionary itself may contain null entries, distinct
  /// from null indices; both decode to null.
  /// </summary>
  private static IArrowArray DecodeDictionary(DictionaryArray array) {
    // The dictionary values could themselves be encoded in principle; decode defensively.
    var dict = Decode(array.Dictionary);
    // EnumeratePhysicalIndices yields one index per logical row, but represents a null
    // index as an arbitrary sentinel, so nullness is taken from the array's own validity.
    var length = array.Length;
    var physicalIndices = new int?[length];
    var i = 0;
    foreach (var physicalIndex in array.EnumeratePhysicalIndices()) {
      physicalIndices[i] = array.IsNull(i) ? null : CheckIndexInRange(physicalIndex, dict);
      ++i;
    }
    return RebuildFromIndices(dict, physicalIndices);
  }

  /// <summary>
  /// Expands a RunEndEncodedArray into a plain array of its values' type. An REE array
  /// carries no validity buffer of its own: nulls live on the values child.
  /// </summary>
  private static IArrowArray DecodeRunEnds(RunEndEncodedArray array) {
    // For RunEndEncoded<Dictionary<...>>, this flattens the values to plain first.
    // Physical indices from the parent then index the decoded values directly, because
    // DecodeDictionary preserves element positions one-for-one.
    var values = Decode(array.Values);
    var length = array.Length;
    var physicalIndices = new int?[length];
    var i = 0;
    foreach (var physicalIndex in array.EnumeratePhysicalIndices()) {
      physicalIndices[i] = CheckIndexInRange(physicalIndex, values);
      ++i;
    }
    return RebuildFromIndices(values, physicalIndices);
  }

  /// <summary>
  /// Checks that an index decoded from the wire is actually in range for 'values'.
  /// A well-formed message never produces one that is not, but if an upstream encoding step
  /// mishandles validity (e.g. writes a null sentinel into an index buffer without setting
  /// the corresponding validity bit), the raw value can be garbage. Fail loudly rather than
  /// index out of bounds.
  /// </summary>
  private static int CheckIndexInRange(int index, IArrowArray values) {
    if (index < 0 || index >= values.Length) {
      throw new Exception(
        $"Encoded-array index {index} is out of range for a values array of length {values.Length}. " +
        "This indicates a corrupt or mismatched encoding on the wire (for example, a null index " +
        "whose validity bit was not set).");
    }
    return index;
  }

  /// <summary>
  /// Builds a well-formed empty array of the same type as 'array', or null if the type is not
  /// one this class knows how to build. Exists because a zero-row variable-width array
  /// deserialized from IPC has zero-length buffers (e.g. no offsets), a shape that
  /// Apache.Arrow's ArrowArrayConcatenator cannot concatenate onto — see FlightIpcReader.
  /// </summary>
  internal static IArrowArray? TryMakeWellFormedEmpty(IArrowArray array) {
    try {
      // With no indices to append, this never reads 'array' and just builds an empty
      // array of its type.
      return RebuildFromIndices(array, []);
    } catch (Exception) {
      // The values type is outside RebuildFromIndices' supported set.
      return null;
    }
  }

  /// <summary>
  /// Builds a plain array whose element at position i is values[physicalIndices[i]],
  /// or null when the index is null or the referenced value is null.
  /// </summary>
  private static IArrowArray RebuildFromIndices(IArrowArray values, int?[] physicalIndices) {
    return values switch {
      StringArray a => Rebuild(a, physicalIndices, new StringArray.Builder(),
        (b, src, i) => b.Append(src.GetString(i)), b => b.AppendNull()),
      BooleanArray a => Rebuild(a, physicalIndices, new BooleanArray.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      UInt16Array a => Rebuild(a, physicalIndices, new UInt16Array.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      Int8Array a => Rebuild(a, physicalIndices, new Int8Array.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      Int16Array a => Rebuild(a, physicalIndices, new Int16Array.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      Int32Array a => Rebuild(a, physicalIndices, new Int32Array.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      Int64Array a => Rebuild(a, physicalIndices, new Int64Array.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      FloatArray a => Rebuild(a, physicalIndices, new FloatArray.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      DoubleArray a => Rebuild(a, physicalIndices, new DoubleArray.Builder(),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      TimestampArray a => Rebuild(a, physicalIndices,
        new TimestampArray.Builder((TimestampType)a.Data.DataType),
        (b, src, i) => b.Append(src.GetTimestamp(i)!.Value), b => b.AppendNull()),
      Date64Array a => Rebuild(a, physicalIndices, new Date64Array.Builder(),
        (b, src, i) => b.Append(src.GetDateTimeOffset(i)!.Value), b => b.AppendNull()),
      Time64Array a => Rebuild(a, physicalIndices,
        new Time64Array.Builder((Time64Type)a.Data.DataType),
        (b, src, i) => b.Append(src.GetValue(i)!.Value), b => b.AppendNull()),
      _ => throw new Exception(
        $"Decoding an encoded column with values of type {values.Data.DataType.Name} is not supported")
    };
  }

  private static IArrowArray Rebuild<TArray, TBuilder>(TArray values, int?[] physicalIndices,
    TBuilder builder, Action<TBuilder, TArray, int> appendFrom, Action<TBuilder> appendNull)
    where TArray : IArrowArray
    where TBuilder : IArrowArrayBuilder<TArray> {
    foreach (var maybeIndex in physicalIndices) {
      if (maybeIndex == null || values.IsNull(maybeIndex.Value)) {
        appendNull(builder);
        continue;
      }
      appendFrom(builder, values, maybeIndex.Value);
    }
    return builder.Build(default);
  }
}
