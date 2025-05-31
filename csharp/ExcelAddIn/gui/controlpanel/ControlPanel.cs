using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

public partial class ControlPanel : Form {
  public readonly EndpointElements Endpoint;
  public readonly StatusElements Status;

  public ControlPanel() {
    InitializeComponent();
    Endpoint = new(this);
    Status = new(this);

    // Set the version label
    versionLabel.Text = Utility.VersionString;

  }

  public partial class EndpointElements;
  public partial class StatusElements;
}

public partial class ControlPanel {
  public partial class EndpointElements {
    public event Action? OnNewButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnDeleteButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnReconnectButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnMakeDefaultButtonClicked;
    public event Action<EndpointManagerDialogRow[]>? OnEditButtonClicked;

    private readonly BindingSource _bindingSource = new();
  }
}

public partial class ControlPanel {
  public partial class StatusElements {
    private readonly ControlPanel _owner;
    private readonly BindingSource _bindingSource = new();
    public event Action<StatusMonitorDialogRow[]>? OnRetryButtonClicked;

    public StatusElements(ControlPanel owner) {
      _owner = owner;
      _bindingSource.DataSource = typeof(StatusMonitorDialogRow);
      _owner.statusDataGrid.DataSource = _bindingSource;

      // Retry button is initially disabled
      _owner.retryButton.Enabled = false;
    }

    public void AddRow(StatusMonitorDialogRow row) {
      if (_owner.InvokeRequired) {
        _owner.Invoke(() => AddRow(row));
        return;
      }
      _bindingSource.Add(row);
      _owner.statusDataGrid.ClearSelection();
    }

    public void RemoveRow(StatusMonitorDialogRow row) {
      if (_owner.InvokeRequired) {
        _owner.Invoke(() => RemoveRow(row));
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
      var sr = _owner.statusDataGrid.SelectedRows;
      var count = sr.Count;
      for (var i = 0; i != count; ++i) {
        result.Add((StatusMonitorDialogRow)sr[i].DataBoundItem);
      }
      return result.ToArray();
    }

    private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
      var someRowsSelected = _owner.statusDataGrid.SelectedRows.Count != 0;
      _owner.retryButton.Enabled = someRowsSelected;
    }
  }
}
