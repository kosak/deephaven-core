using Deephaven.ExcelAddIn.Viewmodels;

namespace Deephaven.ExcelAddIn.Views;

using SelectedRowsAction = Action<ConnectionManagerDialogRow[]>;

public partial class ConnectionManagerDialog : Form {
  private const string IsDefaultColumnName = "IsDefault";
  private readonly Action _onNewButtonClicked;
  private readonly SelectedRowsAction _onDeleteButtonClicked;
  private readonly SelectedRowsAction _onReconnectButtonClicked;
  private readonly SelectedRowsAction _onEditButtonClicked;
  private readonly BindingSource _bindingSource = new();

  public ConnectionManagerDialog(Action onNewButtonClicked,
    SelectedRowsAction onDeleteButtonClicked,
    SelectedRowsAction onReconnectButtonClicked,
    SelectedRowsAction onEditButtonClicked) {
    _onNewButtonClicked = onNewButtonClicked;
    _onDeleteButtonClicked = onDeleteButtonClicked;
    _onReconnectButtonClicked = onReconnectButtonClicked;
    _onEditButtonClicked = onEditButtonClicked;

    InitializeComponent();

    _bindingSource.DataSource = typeof(ConnectionManagerDialogRow);
    dataGridView1.DataSource = _bindingSource;

    dataGridView1.CellClick += DataGridView1_CellClick;
  }

  public void AddRow(ConnectionManagerDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    dataGridView1.ClearSelection();
  }

  public void RemoveRow(ConnectionManagerDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => RemoveRow(row));
      return;
    }
    _bindingSource.Remove(row);
  }

  private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
    // Quite a bit of drama here to support the clicking inside the "IsDefault" checkbox column
    if (e.RowIndex < 0 || e.ColumnIndex < 0) {
      return;
    }

    if (_bindingSource[e.RowIndex] is not ConnectionManagerDialogRow row) {
      return;
    }
    var name = dataGridView1.Columns[e.ColumnIndex].Name;
    if (name == IsDefaultColumnName) {
      Console.WriteLine("TODO");
      // row.IsDefaultClicked();
    }
  }

  private void newButton_Click(object sender, EventArgs e) {
    _onNewButtonClicked();
  }

  private void reconnectButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    _onReconnectButtonClicked(selections);
  }

  private void editButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    _onEditButtonClicked(selections);
  }

  private void deleteButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    _onDeleteButtonClicked(selections);
  }

  private ConnectionManagerDialogRow[] GetSelectedRows() {
    var result = new List<ConnectionManagerDialogRow>();
    var sr = dataGridView1.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((ConnectionManagerDialogRow)sr[i].DataBoundItem);
    }

    return result.ToArray();
  }
}
