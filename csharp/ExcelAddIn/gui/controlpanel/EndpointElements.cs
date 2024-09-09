namespace Deephaven.ExcelAddIn.Gui;

public class EndpointElements {
  public readonly ControlPanel Owner;
  public event Action? OnNewButtonClicked;
  public event Action<EndpointManagerRow[]>? OnDeleteButtonClicked;
  public event Action<EndpointManagerRow[]>? OnReconnectButtonClicked;
  public event Action<EndpointManagerRow[]>? OnMakeDefaultButtonClicked;
  public event Action<EndpointManagerRow[]>? OnEditButtonClicked;

  private readonly BindingSource _bindingSource = new();

  public EndpointElements(ControlPanel owner) {
    Owner = owner;

    _bindingSource.DataSource = typeof(EndpointManagerRow);
    Owner.endpointDataGrid.DataSource = _bindingSource;
    Owner.endpointDataGrid.SelectionChanged += endpointDataGrid_SelectionChanged;

    Owner.deleteButton.Enabled = false;
    Owner.deleteButton.Click += deleteButton_Click;

    Owner.reconnectButton.Enabled = false;
    Owner.reconnectButton.Click += reconnectButton_Click;

    Owner.editButton.Enabled = false;
    Owner.editButton.Click += editButton_Click;

    Owner.makeDefaultButton.Enabled = false;
    Owner.makeDefaultButton.Click += makeDefaultButton_Click;

    Owner.newButton.Click += newButton_Click;
  }

  public void AddRow(EndpointManagerRow row) {
    if (Owner.InvokeRequired) {
      Owner.Invoke(() => AddRow(row));
      return;
    }
    _bindingSource.Add(row);
    Owner.endpointDataGrid.ClearSelection();
  }

  public void RemoveRow(EndpointManagerRow row) {
    if (Owner.InvokeRequired) {
      Owner.Invoke(() => RemoveRow(row));
      return;
    }
    _bindingSource.Remove(row);
  }

  private void newButton_Click(object? sender, EventArgs e) {
    OnNewButtonClicked?.Invoke();
  }

  private void reconnectButton_Click(object? sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnReconnectButtonClicked?.Invoke(selections);
  }

  private void editButton_Click(object? sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnEditButtonClicked?.Invoke(selections);
  }

  private void deleteButton_Click(object? sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnDeleteButtonClicked?.Invoke(selections);
  }

  private void makeDefaultButton_Click(object? sender, EventArgs e) {
    var selections = GetSelectedRows();
    OnMakeDefaultButtonClicked?.Invoke(selections);
  }

  private EndpointManagerRow[] GetSelectedRows() {
    var result = new List<EndpointManagerRow>();
    var sr = Owner.endpointDataGrid.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      result.Add((EndpointManagerRow)sr[i].DataBoundItem);
    }
    return result.ToArray();
  }

  private void endpointDataGrid_SelectionChanged(object? sender, EventArgs e) {
    var numRowsSelected = Owner.endpointDataGrid.SelectedRows.Count;
    var someRowsSelected = numRowsSelected != 0;
    var oneRowSelected = numRowsSelected == 1;

    Owner.deleteButton.Enabled = someRowsSelected;
    Owner.reconnectButton.Enabled = someRowsSelected;
    Owner.editButton.Enabled = someRowsSelected;

    Owner.makeDefaultButton.Enabled = oneRowSelected;
  }
}
