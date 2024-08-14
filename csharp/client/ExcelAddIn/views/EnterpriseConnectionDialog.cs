﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using Deephaven.ExcelAddIn.ViewModels;

namespace Deephaven.ExcelAddIn.Views {
  public partial class EnterpriseConnectionDialog : Form {
    private readonly EnterpriseConnectionDialogViewModel _vm;
    private readonly Action<Form, string, string, string, string, string> _onConnect;

    public EnterpriseConnectionDialog(EnterpriseConnectionDialogViewModel vm,
      Action<Form, string, string, string, string, string> onConnect) {
      _vm = vm;
      _onConnect = onConnect;
      InitializeComponent();
      jsonUrlText.DataBindings.Add("Text", vm, "JsonUrl");
      usernameText.DataBindings.Add("Text", vm, "UserId");
      passwordText.DataBindings.Add("Text", vm, "Password");
      operateAsText.DataBindings.Add("Text", vm, "OperateAs");
      persistentQueryText.DataBindings.Add("Text", vm, "PersistentQuery");
    }

    private void connectButton_Click(object sender, EventArgs e) {
      _onConnect(this, _vm.JsonUrl.Trim(), _vm.UserId.Trim(), _vm.Password.Trim(),
        _vm.OperateAs.Trim(), _vm.PersistentQuery.Trim());
    }
  }
}
