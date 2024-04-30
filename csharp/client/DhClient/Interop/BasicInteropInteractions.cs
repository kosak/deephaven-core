using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace Deephaven.DhClient.Interop;

class Constants {
  public const string DhCorePath = "dhcore.dll";
  public const string DhClientPath = "dhclient.dll";
}

public class BasicInteropInteractions {
  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Add(Int32 a, Int32 b, out Int32 result);

  [DllImport(Constants.DhCorePath, CharSet = CharSet.Unicode)]
  public static extern void deephaven_dhcore_basicInteropInteractions_Concat(string a, string b, out string result);
}
