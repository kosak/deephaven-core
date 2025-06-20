namespace Deephaven.ManagedClient;

public class TableMaker {
  /**
 * Creates a column whose server type most closely matches type T, having the given name and
 * values. Each call to this method adds a column. When there are multiple calls to this method,
 * the sizes of the `values` arrays must be consistent across those calls. That is, when the
 * table has multiple columns, they all have to have the same number of rows.
 */
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
}
