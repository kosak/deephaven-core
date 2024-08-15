using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace ExcelAddIn.views {
  public partial class ConnectionManagerDialog : Form {
    public ConnectionManagerDialog(BindingSource bs) {
      InitializeComponent();

      dataGridView1.DataSource = bs;
      var button = new DataGridViewButtonColumn();
      button.Name = "clicker_button";
      button.HeaderText = "Clicker";
      button.Text = "Clicker2";
      button.UseColumnTextForButtonValue = true;
      dataGridView1.Columns.Add(button);
      dataGridView1.CellClick += DataGridView1_CellClick;
    }

    private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e) {
      var col = e.ColumnIndex;
      var row = e.RowIndex;
      var ccc = dataGridView1.Columns[col];
      var name = ccc.Name;
      Debug.WriteLine($"zamboni click row={row} col={col})");
    }
  }
}
