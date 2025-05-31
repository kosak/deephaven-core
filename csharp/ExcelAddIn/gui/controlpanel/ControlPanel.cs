using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deephaven.ExcelAddIn.Gui;


public partial class ControlPanel : Form {
  public readonly EndpointElements Endpoint = new();
  public readonly StatusElements Status = new();

  public ControlPanel() {
    InitializeComponent();
  }
}


public class EndpointElements {
  public event Action? OnNewButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnDeleteButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnReconnectButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnMakeDefaultButtonClicked;
  public event Action<EndpointManagerDialogRow[]>? OnEditButtonClicked;

  private readonly BindingSource _bindingSource = new();
}

public class StatusElements {
  public event Action<StatusMonitorDialogRow[]>? OnRetryButtonClicked;

  private readonly BindingSource _bindingSource = new();
}
