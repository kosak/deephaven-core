namespace Deephaven.ExcelAddIn.Gui;

using SelectedRowsAction = Action<StatusMonitorDialogRow[]>;

public partial class StatusMonitorDialog : Form {
  public event SelectedRowsAction? OnRetryButtonClicked;

  private readonly BindingSource _bindingSource = new();

  public StatusMonitorDialog() {
    InitializeComponent();

    _bindingSource.DataSource = typeof(StatusMonitorDialogRow);
    dataGridView1.DataSource = _bindingSource;
  }

  public void AddRow(StatusMonitorDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    dataGridView1.ClearSelection();
  }

  public void RemoveRow(StatusMonitorDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => RemoveRow(row));
      return;
    }
    _bindingSource.Remove(row);
  }

  private void retryButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnRetryButtonClicked?.Invoke(selections);
  }

  private StatusMonitorDialogRow[] GetSelectedRows() {
    var result = new List<StatusMonitorDialogRow>();
    var sr = dataGridView1.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((StatusMonitorDialogRow)sr[i].DataBoundItem);
    }
    return result.ToArray();
  }
}
