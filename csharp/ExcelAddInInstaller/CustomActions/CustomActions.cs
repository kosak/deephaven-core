using System;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public static class ErrorCodes {
    // There are many, many possible error codes.
    public const int Success = 0;
    public const int Failure = 1603;
  }
  public static class CustomActions {
    public static int RegisterAddIn(string msiHandle) {
      return RunHelper(msiHandle, "RegisterAddIn", sess => DoRegisterAddIn(sess, true));
    }

    public static int UnregisterAddIn(string msiHandle) {
      return RunHelper(msiHandle, "UnregisterAddIn", sess => DoRegisterAddIn(sess, false));
    }

    private static int RunHelper(string msiHandle, string what, Action<MsiSession> action) {
      // First try to get a session
      MsiSession session;
      try {
        session = new MsiSession(msiHandle);
      } catch (Exception) {
        // Didn't get very far
        return ErrorCodes.Failure;
      }

      // Now that we have a session, we can log failures to the session if we need to
      try {
        session.Log($"{what} starting", MsiSession.InstallMessage.INFO);
        action(session);
        session.Log($"{what} completed successfully", MsiSession.InstallMessage.INFO);
        return ErrorCodes.Success;
      } catch (Exception ex) {
        session.Log(ex.Message, MsiSession.InstallMessage.ERROR);
        session.Log($"{what} exited with error", MsiSession.InstallMessage.ERROR);
        return ErrorCodes.Failure;
      }
    }

    private static void DoRegisterAddIn(MsiSession session, bool wantAddIn) {
      var addInName = session.CustomActionData;
      session.Log($"DoRegisterAddIn({wantAddIn}) with addin={addInName}", MsiSession.InstallMessage.INFO);
      if (string.IsNullOrEmpty(addInName)) {
        throw new ArgumentException("Expected addin path, got null or empty");
      }

      Action<string> logger = s => session.Log(s, MsiSession.InstallMessage.INFO);

      if (!RegistryManager.TryMakeAddInEntryFromPath(addInName, out var addInEntry, out var failureReason) ||
          !RegistryManager.TryCreate(logger, out var rm, out failureReason) ||
          !rm.TryUpdateAddInKeys(addInEntry, wantAddIn, out failureReason)) {
        throw new Exception(failureReason);
      }
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
