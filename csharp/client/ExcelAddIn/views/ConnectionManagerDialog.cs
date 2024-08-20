namespace Deephaven.ExcelAddIn.Views;

public partial class ConnectionManagerDialog : Form {
  private const string SettingsButtonColumnName = "settings_button_column";
  private const string ReconnectButtonColumnName = "reconnect_button_column";
  private readonly BindingSource _bindingSource = new();
  private readonly List<Action> _settingsActions = new();
  private readonly List<Action> _reconnectActions = new();

  public ConnectionManagerDialog() {
    InitializeComponent();

    _bindingSource.DataSource = typeof(HyperZamboniRow);
    dataGridView1.DataSource = _bindingSource;
    var settingsButtonColumn = new DataGridViewButtonColumn {
      Name = SettingsButtonColumnName,
      HeaderText = "Settings",
      Text = "Change",
      UseColumnTextForButtonValue = true
    };
    var reconnectButtonColumn = new DataGridViewButtonColumn {
      Name = ReconnectButtonColumnName,
      HeaderText = "Reconnect",
      Text = "Reconnect",
      UseColumnTextForButtonValue = true
    };


    dataGridView1.Columns.Add(settingsButtonColumn);
    dataGridView1.Columns.Add(reconnectButtonColumn);

    dataGridView1.CellClick += DataGridView1_CellClick;
  }

  public void AddRow(HyperZamboniRow row, Action settingsAction, Action reconnectAction) {
    _bindingSource.Add(row);
    _settingsActions.Add(settingsAction);
    _reconnectActions.Add(reconnectAction);
  }

  private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
    if (e.RowIndex < 0) {
      return;
    }

    var name = dataGridView1.Columns[e.ColumnIndex].Name;
    if (name == SettingsButtonColumnName) {
      _settingsActions[e.RowIndex]();
    } else if (name == ReconnectButtonColumnName) {
      _reconnectActions[e.RowIndex]();
    }
  }
}
