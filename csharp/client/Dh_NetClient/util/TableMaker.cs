namespace Deephaven.ManagedClient;

#if false

public class TableMaker {

  /// <summary>
  /// Creates a column whose server type most closely matches type T, having the given name and
  /// values.Each call to this method adds a column. When there are multiple calls to this method,
  /// the sizes of the `values` arrays must be consistent across those calls. That is, when the
  /// table has multiple columns, they all have to have the same number of rows.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="name"></param>
  /// <param name="values"></param>
  public void AddColumn<T>(string name, IEnumerable<T> values) {
    var cb = new ColumnBuilder<T>();
    foreach (var element in values) {
      cb.Append(element);
    }
    var array = cb.Finish();
    // var (typeName, componentTypeName) = cb.GetDeephavenMetadata();

    // var kvMetadata = new KeyValueMetadata();
    // OkOrThrow(DEEPHAVEN_LOCATION_EXPR(
    //   kv_metadata->Set(DeephavenMetadataConstants::Keys::Type(), std::move(deephaven_metadata_type_name))));
    // if (deephaven_metadata_component_type_name.has_value()) {
    //   OkOrThrow(DEEPHAVEN_LOCATION_EXPR(
    //     kv_metadata->Set(DeephavenMetadataConstants::Keys::ComponentType(),
    //       std::move(*deephaven_metadata_component_type_name))));
    // }

    _columnInfos.Add(std::move(name), data->type(), std::move(kv_metadata),
      std::move(data));

  }

  private class ColumnInfo {
    private Apache.Arrow.Date32Array d;
    std::string name_;
    std::shared_ptr<arrow::DataType> arrow_type_;
    std::shared_ptr<arrow::KeyValueMetadata> arrow_metadata_;
    std::shared_ptr<arrow::Array> data_;

    ColumnInfo(std::string name, std::shared_ptr<arrow::DataType> arrow_type,
      std::shared_ptr<arrow::KeyValueMetadata> arrow_metadata,
      std::shared_ptr<arrow::Array> data);
    ColumnInfo(ColumnInfo &&other) noexcept;
    ~ColumnInfo();

    public void Foo() {
      Apache.Arrow.Date32Array

    }

  };

}


#endif

