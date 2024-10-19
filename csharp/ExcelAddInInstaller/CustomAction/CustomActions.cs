using System;
using System.Diagnostics;

namespace CustomAction {
  public class CustomActions {
    public static int CustomAction1(string aMsiHandle) {
      MsiSession session = new MsiSession(aMsiHandle);

      // This will be used to show the message box or any dialog modal to installer window
      Win32Window msiWindow = new Win32Window(session.GetMsiWindowHandle());

      var megaId = Process.GetCurrentProcess().Id;

      System.Windows.Forms.MessageBox.Show(msiWindow, $"(Don't) attach your debugger now! ({megaId})", "Advanced Installer custom action");

      // Log data passed as action data
      string infoMessage = string.Format("User data: \"{0}\"", session.CustomActionData);
      session.Log(infoMessage, MsiSession.InstallMessage.INFO);

      // Get property value
      string myProperty = "APPDIR";
      string myPropertyValue = session.GetProperty(myProperty);

      // Log property MY_PROPERTY value

      System.Windows.Forms.MessageBox.Show(msiWindow, myPropertyValue, "This is what I found");

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

      // Failure
      // return 1603;
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
