using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

using SelectedRowsAction = Action<EndpointManagerDialogRow[]>;

public partial class EndpointManagerDialog : Form {
  public event Action? OnNewButtonClicked;
  public event SelectedRowsAction? OnDeleteButtonClicked;
  public event SelectedRowsAction? OnReconnectButtonClicked;
  public event SelectedRowsAction? OnMakeDefaultButtonClicked;
  public event SelectedRowsAction? OnEditButtonClicked;

  private readonly BindingSource _bindingSource = new();

  public EndpointManagerDialog() {
    InitializeComponent();

    _bindingSource.DataSource = typeof(EndpointManagerDialogRow);
    dataGridView1.DataSource = _bindingSource;
    versionLabel.Text = Utility.VersionString;

    deleteButton.Enabled = false;
    reconnectButton.Enabled = false;
    editButton.Enabled = false;
    makeDefaultButton.Enabled = false;
  }

  public void AddRow(EndpointManagerDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    dataGridView1.ClearSelection();
  }

  public void RemoveRow(EndpointManagerDialogRow row) {
    if (InvokeRequired) {
      Invoke(() => RemoveRow(row));
      return;
    }
    _bindingSource.Remove(row);
  }

  private void newButton_Click(object sender, EventArgs e) {
    OnNewButtonClicked?.Invoke();
  }

  private void reconnectButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnReconnectButtonClicked?.Invoke(selections);
  }

  private void editButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnEditButtonClicked?.Invoke(selections);
  }

  private void deleteButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnDeleteButtonClicked?.Invoke(selections);
  }

  private void makeDefaultButton_Click(object sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnMakeDefaultButtonClicked?.Invoke(selections);
  }

  private EndpointManagerDialogRow[] GetSelectedRows() {
    var result = new List<EndpointManagerDialogRow>();
    var sr = dataGridView1.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((EndpointManagerDialogRow)sr[i].DataBoundItem);
    }
    return result.ToArray();
  }

  private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
    var numRowsSelected = dataGridView1.SelectedRows.Count;
    var someRowsSelected = numRowsSelected != 0;
    var oneRowSelected = numRowsSelected == 1;

    deleteButton.Enabled = someRowsSelected;
    reconnectButton.Enabled = someRowsSelected;
    editButton.Enabled = someRowsSelected;

    makeDefaultButton.Enabled = oneRowSelected;
  }
}
