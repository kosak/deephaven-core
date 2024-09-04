using Deephaven.ExcelAddIn.Viewmodels;

namespace Deephaven.ExcelAddIn.Views;

using SelectedRowsAction = Action<ConnectionManagerDialogRow[]>;

public partial class ConnectionManagerDialog : Form {
  private const string IsDefaultColumnName = "IsDefault";
  private readonly Action _onNewButtonClicked;
  private readonly Action _onDeleteButtonClicked;
  private readonly Action _onReconnectButtonClicked;
  private readonly Action _onEditButtonClicked;
  private readonly BindingSource _bindingSource = new();

  public ConnectionManagerDialog(Action onNewButtonClicked,
    SelectedRowsAction onDeleteButtonClicked,
    SelectedRowsAction onReconnectButtonClicked,
    SelectedRowsAction onEditButtonClicked) {
    _onNewButtonClicked = onNewButtonClicked;

    InitializeComponent();

    _bindingSource.DataSource = typeof(ConnectionManagerDialogRow);
    dataGridView1.DataSource = _bindingSource;

    var settingsButtonColumn = new DataGridViewButtonColumn {
      Name = SettingsButtonColumnName,
      HeaderText = "Credentials",
      Text = "Edit",
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

  public void AddRow(ConnectionManagerDialogRow row) {
    _bindingSource.Add(row);
  }

  private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
    if (e.RowIndex < 0 || e.ColumnIndex < 0) {
      return;
    }

    if (_bindingSource[e.RowIndex] is not ConnectionManagerDialogRow row) {
      return;
    }
    var name = dataGridView1.Columns[e.ColumnIndex].Name;

    switch (name) {
      case SettingsButtonColumnName: {
          row.SettingsClicked();
          break;
        }

      case ReconnectButtonColumnName: {
          row.ReconnectClicked();
          break;
        }

      case IsDefaultColumnName: {
          row.IsDefaultClicked();
          break;
        }
    }
  }

  private void newButton_Click(object sender, EventArgs e) {
    _onNewButtonClicked();
  }

  private void reconnectButton_Click(object sender, EventArgs e) {
    var selections = new List<ConnectionManagerDialogRow>();
    var sr = dataGridView1.SelectedRows;
    var count = sr.Count;
    for (var i = 0; i != count; ++i) {
      selections.Add((ConnectionManagerDialogRow)sr[i].DataBoundItem);
    }
    Console.WriteLine("FUN");
  }
}
