namespace Deephaven.ExcelAddIn.Gui;

public class StatusElements {
  private readonly ControlPanel _owner;
  private readonly BindingSource _bindingSource = new();
  public event Action<StatusMonitorRow[]>? OnRetryButtonClicked;

  public StatusElements(ControlPanel owner) {
    _owner = owner;
    _bindingSource.DataSource = typeof(StatusMonitorRow);
    _owner.statusDataGrid.DataSource = _bindingSource;
    _owner.statusDataGrid.SelectionChanged += statusDataGrid_SelectionChanged;

    // Retry button is initially disabled
    _owner.retryButton.Enabled = false;
    _owner.retryButton.Click += retryButton_Click;
  }

  public void AddRow(StatusMonitorRow row) {
    if (_owner.InvokeRequired) {
      _owner.Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    _owner.statusDataGrid.ClearSelection();
  }

  public void RemoveRow(StatusMonitorRow row) {
    if (_owner.InvokeRequired) {
      _owner.Invoke(() => RemoveRow(row));
      return;
    }
    _bindingSource.Remove(row);
  }

  private void retryButton_Click(object? sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnRetryButtonClicked?.Invoke(selections);
  }

  private StatusMonitorRow[] GetSelectedRows() {
    var result = new List<StatusMonitorRow>();
    var sr = _owner.statusDataGrid.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((StatusMonitorRow)sr[i].DataBoundItem);
    }
    return result.ToArray();
  }

  private void statusDataGrid_SelectionChanged(object? sender, EventArgs e) {
    var someRowsSelected = _owner.statusDataGrid.SelectedRows.Count != 0;
    _owner.retryButton.Enabled = someRowsSelected;
  }
}
