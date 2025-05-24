namespace Deephaven.ExcelAddIn.Gui {
  public partial class StatusMonitorDialog : Form {
    private readonly BindingSource _bindingSource = new();

    public StatusMonitorDialog() {
      InitializeComponent();

      _bindingSource.DataSource = typeof(StatusMonitorDialogRow);
      dataGridView1.DataSource = _bindingSource;

      var nubbinRow = new StatusMonitorDialogRow(1234, "how many");
      nubbinRow.Status = "hello";
      nubbinRow.Severity = true;
      _bindingSource.Add(nubbinRow);
    }
  }
}

