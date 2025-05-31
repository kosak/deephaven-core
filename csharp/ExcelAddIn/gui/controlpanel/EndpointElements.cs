namespace Deephaven.ExcelAddIn.Gui;

public partial class ControlPanel {
  public partial class EndpointElements {
    private readonly ControlPanel _owner;
    public event Action? OnNewButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnDeleteButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnReconnectButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnMakeDefaultButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnEditButtonClicked;

    private readonly BindingSource _bindingSource = new();

    public EndpointElements(ControlPanel owner) {
      _owner = owner;

      _bindingSource.DataSource = typeof(EndpointManagerDialogRow);
      _owner.endpointDataGrid.DataSource = _bindingSource;

      _owner.deleteButton.Enabled = false;
      _owner.reconnectButton.Enabled = false;
      _owner.editButton.Enabled = false;
      _owner.makeDefaultButton.Enabled = false;
    }

    public void AddRow(EndpointManagerDialogRow row) {
      if (_owner.InvokeRequired) {
        _owner.Invoke(() => AddRow(row));
        return;
      }
      _bindingSource.Add(row);
      _owner.endpointDataGrid.ClearSelection();
    }

    public void RemoveRow(EndpointManagerDialogRow row) {
      if (_owner.InvokeRequired) {
        _owner.Invoke(() => RemoveRow(row));
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
      var sr = _owner.endpointDataGrid.SelectedRows;
      var count = sr.Count;
      for (var i = 0; i != count; ++i) {
        result.Add((EndpointManagerDialogRow)sr[i].DataBoundItem);
      }
      return result.ToArray();
    }

    private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
      var numRowsSelected = _owner.endpointDataGrid.SelectedRows.Count;
      var someRowsSelected = numRowsSelected != 0;
      var oneRowSelected = numRowsSelected == 1;

      _owner.deleteButton.Enabled = someRowsSelected;
      _owner.reconnectButton.Enabled = someRowsSelected;
      _owner.editButton.Enabled = someRowsSelected;

      _owner.makeDefaultButton.Enabled = oneRowSelected;
    }
  }
}
