namespace Deephaven.ManagedClient;

public class TickingUpdate(
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace,
  ClientTable afterAdds,
  RowSequence[] modifiedRowsIndexSpace,
  ClientTable afterModifies) {
  public ClientTable Prev => prev;
  public RowSequence RemovedRowsIndexSpace => removedRowsIndexSpace;
  public ClientTable AfterRemoves => afterRemoves;
  public RowSequence AddedRowsIndexSpace => addedRowsIndexSpace;
  public ClientTable AfterAdds => afterAdds;
  public RowSequence[] ModifiedRowsIndexSpace => modifiedRowsIndexSpace;
  public ClientTable AfterModifies => afterModifies;
}
