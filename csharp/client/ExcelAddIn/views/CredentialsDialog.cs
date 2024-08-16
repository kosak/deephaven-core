using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Deephaven.ExcelAddIn.ViewModels;

namespace ExcelAddIn.views {
  public partial class CredentialsDialog : Form {
    public CredentialsDialog(CredentialsDialogViewModel vm) {
      InitializeComponent();
      // Need to fire these bindings on property changed rather than simply on validation,
      // because on validation is not responsive enough. Also, painful technical note:
      // being a member of connectionTypeGroup *also* ensures that at most one of these buttons
      // are checked. So you might think databinding is not necessary. However being in
      // a group does nothing for the initial conditions. So the group doesn't care if
      // *neither* of them are checked.
      isCorePlusRadioButton.DataBindings.Add(new Binding("Checked", vm, "IsCorePlus", false, DataSourceUpdateMode.OnPropertyChanged));
      isCoreRadioButton.DataBindings.Add(new Binding("Checked", vm, "IsCore", false, DataSourceUpdateMode.OnPropertyChanged));

      // Make one of the two panels visible, according to the setting of the radio box.
      corePlusPanel.DataBindings.Add("Visible", vm, "IsCorePlus");
      corePanel.DataBindings.Add("Visible", vm, "IsCore");
    }

    private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e) {

    }

    private void radioButton1_CheckedChanged(object sender, EventArgs e) {

    }

    private void radioButton2_CheckedChanged(object sender, EventArgs e) {

    }

    private void label2_Click(object sender, EventArgs e) {

    }
  }
}
