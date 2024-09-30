namespace Deephaven.ManagedClient;

public class TickingUpdate(
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace,
  ClientTable afterAdds,
  RowSequence[] modifiedRowsIndexSpace,
  ClientTable afterModifies) {

}
