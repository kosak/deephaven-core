namespace Deephaven.ExcelAddIn.Gui;

public class EndpointElements {
  public readonly ControlPanel Owner;
  public event Action? OnNewButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnDeleteButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnReconnectButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnMakeDefaultButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnEditButtonClicked;

  private readonly BindingSource _bindingSource = new();

  public EndpointElements(ControlPanel owner) {
    Owner = owner;

    _bindingSource.DataSource = typeof(EndpointManagerDialogRow);
    Owner.endpointDataGrid.DataSource = _bindingSource;

    Owner.deleteButton.Enabled = false;
    Owner.reconnectButton.Enabled = false;
    Owner.editButton.Enabled = false;
    Owner.makeDefaultButton.Enabled = false;
  }

  public void AddRow(EndpointManagerDialogRow row) {
    if (Owner.InvokeRequired) {
      Owner.Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    Owner.endpointDataGrid.ClearSelection();
  }

  public void RemoveRow(EndpointManagerDialogRow row) {
    if (Owner.InvokeRequired) {
      Owner.Invoke(() => RemoveRow(row));
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
    var sr = Owner.endpointDataGrid.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((EndpointManagerDialogRow)sr[i].DataBoundItem);
    }
    return result.ToArray();
  }

  private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
    var numRowsSelected = Owner.endpointDataGrid.SelectedRows.Count;
    var someRowsSelected = numRowsSelected != 0;
    var oneRowSelected = numRowsSelected == 1;

    Owner.deleteButton.Enabled = someRowsSelected;
    Owner.reconnectButton.Enabled = someRowsSelected;
    Owner.editButton.Enabled = someRowsSelected;

    Owner.makeDefaultButton.Enabled = oneRowSelected;
  }
}
