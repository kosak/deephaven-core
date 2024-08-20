namespace Deephaven.ExcelAddIn.Views;

public partial class ConnectionManagerDialog : Form {
  private const string SettingsButtonColumnName = "settings_button_column";
  private const string ReconnectButtonColumnName = "reconnect_button_column";
  private readonly BindingSource _bindingSource = new();

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

  public void AddRow(HyperZamboniRow row) {
    _bindingSource.Add(row);
  }

  private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
    if (e.RowIndex < 0) {
      return;
    }

    if (_bindingSource[e.RowIndex] is not HyperZamboniRow row) {
      return;
    }
    var name = dataGridView1.Columns[e.ColumnIndex].Name;
    if (name == SettingsButtonColumnName) {
      row.SettingsClicked();
    } else if (name == ReconnectButtonColumnName) {
      row.ReconnectClicked();
    }
  }
}
