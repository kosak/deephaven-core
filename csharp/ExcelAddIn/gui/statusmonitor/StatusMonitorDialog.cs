using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui {
  public partial class StatusMonitorDialog : Form {
    private readonly BindingSource _bindingSource = new();

    public StatusMonitorDialog() {
      InitializeComponent();

      _bindingSource.DataSource = typeof(StatusMonitorDialogRow);
      dataGridView1.DataSource = _bindingSource;

      var opStatus = new OpStatus("func()", StatusOr<Unit>.OfTransient("processing"));
      var nubbinRow = new StatusMonitorDialogRow(opStatus);
      _bindingSource.Add(nubbinRow);
    }

    public void AddRow(StatusMonitorDialogRow row) {
      if (InvokeRequired) {
        Invoke(() => AddRow(row));
        return;
      }
      _bindingSource.Add(row);
      dataGridView1.ClearSelection();
    }

    public void RemoveRow(StatusMonitorDialogRow row) {
      if (InvokeRequired) {
        Invoke(() => RemoveRow(row));
        return;
      }
      _bindingSource.Remove(row);
    }
  }
}

