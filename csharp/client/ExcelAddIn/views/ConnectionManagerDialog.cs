using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace Deephaven.ExcelAddIn.Views;

public partial class ConnectionManagerDialog : Form {
  private const string ClickableColumnName = "clickable_column_button";
  private readonly BindingSource _bindingSource = new();
  private readonly List<Action> _clickActions = new();

  public ConnectionManagerDialog() {
    InitializeComponent();

    _bindingSource.DataSource = typeof(HyperZamboniRow);
    dataGridView1.DataSource = _bindingSource;
    var buttonColumn = new DataGridViewButtonColumn();
    buttonColumn.Name = ClickableColumnName;
    buttonColumn.HeaderText = "Settings";
    buttonColumn.Text = "Change";
    buttonColumn.UseColumnTextForButtonValue = true;
    dataGridView1.Columns.Add(buttonColumn);
    dataGridView1.CellClick += DataGridView1_CellClick;
  }

  public void AddRow(HyperZamboniRow row, Action onClick) {
    _bindingSource.Add(row);
    _clickActions.Add(onClick);
  }

  private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
    var x = dataGridView1.Columns[e.ColumnIndex].Name;
    if (e.RowIndex < 0 || dataGridView1.Columns[e.ColumnIndex].Name != ClickableColumnName) {
      return;
    }

    _clickActions[e.RowIndex]();
  }
}
