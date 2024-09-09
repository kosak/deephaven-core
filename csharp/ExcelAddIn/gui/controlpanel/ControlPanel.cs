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
}
