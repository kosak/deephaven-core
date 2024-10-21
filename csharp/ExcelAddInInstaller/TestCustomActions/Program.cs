using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public class Program {
    static void Main(string[] args) {
      var temp = RegistryManager.TryCreate(out var oem, out var failureReason);
      var stupid = oem.TryCanonicalize("zamboni 666", false, out failureReason);
    }
  }
}
