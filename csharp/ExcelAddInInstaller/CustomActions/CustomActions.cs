using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public static class CustomActions {
    public static bool TryDetermineBitness(out bool is64Bit, out string failureReason) {
      is64Bit = false;
      failureReason = "";
      var regView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
      var regBase = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, regView);
      var subKey = regBase.OpenSubKey(RegistryKeys.Bitness.Key, false);
      if (subKey == null) {
        failureReason = $"Couldn't find registry key {RegistryKeys.Bitness.Key}";
        return false;
      }

      var bitnessValue = subKey.GetValue(RegistryKeys.Bitness.Name);
      if (bitnessValue == null) {
        failureReason = $"Couldn't find entry for {RegistryKeys.Bitness.Name}";
        return false;
      }

      if (bitnessValue.Equals(RegistryKeys.Bitness.Value64)) {
        is64Bit = true;
        return true;
      }

      if (bitnessValue.Equals(RegistryKeys.Bitness.Value32)) {
        is64Bit = false;
        return true;
      }

      failureReason = $"Unexpected bitness value {bitnessValue}";
      return false;
    }

    public static int CustomAction1(string aMsiHandle) {
      MsiSession session = new MsiSession(aMsiHandle);

      // This will be used to show the message box or any dialog modal to installer window
      Win32Window msiWindow = new Win32Window(session.GetMsiWindowHandle());

      System.Windows.Forms.MessageBox.Show(msiWindow, "Attach your debugger now!", "Advanced Installer custom action");

      // Log data passed as action data
      string infoMessage = string.Format("User data: \"{0}\"", session.CustomActionData);
      session.Log(infoMessage, MsiSession.InstallMessage.INFO);

      // Get property value
      string myProperty = "MY_PROPERTY";
      string myPropertyValue = session.GetProperty(myProperty);

      // Log property MY_PROPERTY value
      infoMessage = string.Format("Property \"{0}\" has value: \"{1}\"", myProperty, myPropertyValue);
      session.Log(infoMessage, MsiSession.InstallMessage.INFO);

      string mySecondProperty = "MY_SECOND_PROPERTY";
      string mySecondPropertyValue = string.IsNullOrEmpty(myPropertyValue)
        ? "Advanced"
        : "Installer";

      // Set property value
      session.SetProperty(mySecondProperty, mySecondPropertyValue);

      // Log property MY_SECOND_PROPERTY value update
      infoMessage = string.Format("Property \"{0}\" was set as \"{1}\"", mySecondProperty, mySecondPropertyValue);
      session.Log(infoMessage, MsiSession.InstallMessage.INFO);

      return 0;
    }
  }

  public class Win32Window : System.Windows.Forms.IWin32Window {
    public IntPtr Handle { get; private set; }

    public Win32Window(IntPtr aWindowHandle) {
      Handle = aWindowHandle;
    }
  }
}
