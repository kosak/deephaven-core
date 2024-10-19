# The Custom Actions LIbrary

This library was created by adding a Visual Studio Extension
from Advanced Installer to Visual Studio. The process for adding
the extension is documented here:

https://www.advancedinstaller.com/user-guide/create-dot-net-ca.html

Basically the steps are:
* Open Visual Studio and navigate to Extensions → Manage Extensions.
* In the Online section, search for Advanced Installer for Visual Studio
* In Visual Studio navigate to File → New Project
* From the list of templates, select the C# Custom Action template or the
  C# Custom Action (.NET Framework) template, depending on your needs

For now, I have chosen to create C# Custom Action (.NET Framework) because
that gives me .NET 4.8.0, which, although it is old, is guaranteeed to be
installed on Windows 10/11. (I believe).

The purpose of having a Custom Action is so we can manipulate the registry
settings for Excel. Specifically we care about the following keys:


1. HKEY_LOCAL_MACHINE\Software\Microsoft\Office\${VERSION}\Outlook

(yes Outlook, not Excel)
which contains the name "Bitness" and the values "x86" or "x64".

This helps us determine whether the user has installed the 32-bit vs
64-bit version of Office.

When I say ${VERSION} I mean one of the known versions of Office, one of
the strings in the set 11.0, 12.0, 14.0, 15.0, 16.0

It appears that 16.0 covers Office 2016, 2019, and 2021 and Office 365, so
for Deephaven purposes we probably only care about finding 16.0 and ignoring
any older version we might come across on the target machine.

2. HKEY_CURRENT_USER\Software\Microsoft\Office\$VERSION\Excel\Options

which contains zero or more entries indicating which addins Excel should load
when it starts. These entries have the following keys, which follow the
almost-regular pattern:

OPEN, OPEN1, OPEN2, OPEN3, ...

I say "almost-regular" because key that you might expect to be named OPEN0 is
instead named simply OPEN.

These keys must be kept dense. That is, if you delete some key that is not
at the end of the sequence, you will need to move the later entries down
to fill in the gap. (e.g. the entry keyed by OPEN2 becomes OPEN1 etc).

The value of these entries is the string /R "$FULLPATHTOXLL"

including the space and the quotation marks. On my computer the value of OPEN
is currently

/R "C:\Users\kosak\Desktop\exceladdin-v7\ExcelAddIn-AddIn64-packed.xll"

The fact that I have installed my addin on the Desktop is not a best practice.
The point here is to show the syntax.
