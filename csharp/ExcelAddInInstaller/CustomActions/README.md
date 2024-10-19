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
settings for Excel. Specifically
