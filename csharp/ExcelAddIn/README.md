# Building the Excel Add-In on Windows 10 / 11.

These instructions show how to install and run the Deephaven Excel Add-In
on Windows. These instructions also happen to build the Deephaven C# Client as a
side-effect. However if your goal is to build the Deephaven C# Client,
please see [repository root]/csharp/client/README.md (does not exist yet).

We have tested these instructions on Windows 10 and 11 with Visual Studio
Community Edition.

# Before using the Excel Add-In

To actually use the Deephaven Excel Add-In, you will eventually need to have
at least one Community Core or Enterprise Core+ server running. You don't need
the server yet, and you can successfully follow these build instructions
without a server. However, you will eventually need a server when you want to
run it.

If you don't have a Deephaven Community Core server installation,
you can use these instructions to build one.
https://deephaven.io/core/docs/how-to-guides/launch-build/
For Deephaven Enterprise Core+, contact your administrator.

Furthermore, note that it is only possible to build a server on Linux.
Building a server on Windows is not currently supported.

# Building the Excel Add-In on Windows 10 / Windows 11

## Prerequisites

## Tooling

1. You will need a recent version of Excel installed. We recommend Office 21
   or Office 365. Note that the Add-In only works with installed versions of
   Excel. It does not work with the browser-based web version.

2. Install the .NET Core SDK, version 8.0

   Look for the "Windows | x64" link at
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0

3. Install Visual Studio 2022 Community Edition (or Professional, or Enterprise)
   from here:

   https://visualstudio.microsoft.com/downloads/

   When the installer runs, select both workloads
   "Desktop development with C++" and ".NET desktop development".

   If Visual Studio is already installed, use Tools -> Get Tools and Features
   to add those workloads if necessary.

4. Use your preferred version of git, or install Git from here:

   https://git-scm.com/download/win

## C++ client

The Deephaven Excel Add-In relies on the Deephaven C# Client, which in turn
requires the Deephaven C++ Client (Community Core version). To use Enterprise
Core+ features, it also requires the Deephaven C++ Client (Enterprise Core+
version).

### Build the Deephaven C++ Client (Community Core version)

Follow the instructions at [repository root]/cpp-client/README.md under the
section, under "Building the C++ client on Windows 10 / Windows 11".

When that process is done, you will have C++ client binaries in a
directory referred to by the DHINSTALL environment variable.

### (Optional) build the Deephaven C++ Client (Enterprise Core+ version)

To access Enterprise features, build the Enterprise Core+ version as well.
It will also store its binaries in the same DHINSTALL directory.

(instructions TODO)

## Build the Excel Add-In and C# Add-In

You can build the Add-In from inside Visual Studio or from the Visual Studio
Command Prompt.

### From within Visual Studio

1. Open the Visual Studio solution file
[repository root]\csharp\ExcelAddIn\ExcelAddIn.sln

2. Click on BUILD -> Build solution

### From the Visual Studio Command Prompt

```
cd [repository root]\csharp\ExcelAddIn
devenv ExcelAddIn.sln /build Release
```

## Run the Add-In

### From within Visual Studio

1. In order to actually function, the Add-In requires the C++ Client binaries
   built in the above steps. The easiest thing to do is simply copy all the
   binaries into your Visual Studio build directory:

Assuming a Debug build:

copy -Y %DHINSTALL%\bin [repository root]\csharp\ExcelAddIn\bin\Debug\net8.0-windows

If you are doing a Release build, change "Debug" to "Release" in the above path.

2. Inside Visual Studio Select Debug -> Start Debugging


### From standalone Excel

The steps 






6. Build the C++ 


[repository root]/csharp/client/README.md (does not exist yet).


4. Mkae

3. We will do the actual build process inside a Visual Studio developer
   command prompt. Run the developer command prompt by navigating here:

   Start -> V -> Visual Studio 2022 -> Developer Command Prompt for VS 2022

4. Make a 'dhsrc' directory that will hold the two repositories: the vcpkg
   package manager and Deephaven Core. Then make a 'dhinstall' directory that
   will hold the libraries and executables that are the result of this
   build process.  You can decide on the locations you want for those directories,
   the code below creates them under the home directory of the Windows user
   running the command prompt; change the definitions of the environment variables
   DHSRC and DHINSTALL if you decide to place them somewhere else.
   
   ```
   set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
   set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
   mkdir %DHSRC%
   mkdir %DHINSTALL%
   ```

5. Use git to clone the two repositories mentioned above.
   If you are using Git for Windows, you can run the "Git Bash Shell"
   and type these commands into it:
   ```
   cd $HOME/dhsrc  # change if dhsrc on a different location
   git clone https://github.com/microsoft/vcpkg.git
   git clone https://github.com/deephaven/deephaven-core.git
   ```

6. Come back to the Visual Studio developer command prompt and do the
   one-time installation steps for vcpkg.
   ```
   cd /d %DHSRC%\vcpkg
   .\bootstrap-vcpkg.bat
   ```

7. Set VCPKG_ROOT. Note that steps 8 and 9 both rely on it being set correctly.
   If you come back to these instructions at a future date, make sure that VCPKG_ROOT
   is set before re-running those steps.
   ```
   set VCPKG_ROOT=%DHSRC%\vcpkg
   ```

8. Change to the Deephaven core directory and build/install the dependent
   packages. On my computer this process took about 20 minutes.
   ```
   cd /d %DHSRC%\deephaven-core\cpp-client\deephaven
   %VCPKG_ROOT%\vcpkg.exe install --triplet x64-windows
    ```

9. Now configure the build for Deephaven Core:
   ``` 
   cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%/scripts/buildsystems/vcpkg.cmake -DCMAKE_INSTALL_PREFIX=%DHINSTALL% -DX_VCPKG_APPLOCAL_DEPS_INSTALL=ON
   ```
   
10. Finally, build and install Deephaven Core. Note that the build type (RelWithDebInfo) is specified differently for the Windows build
    than it is for the Ubuntu build. For Windows, we specify the configuration type directly in the build step using the --config flag.
   ```
   # Replace '16' by the number of CPU threads you want to use for building
   cmake --build build --config RelWithDebInfo --target install -- /p:CL_MPCount=16 -m:1
   ```

11. Run the tests.
    First, make sure Deephaven is running. If your Deephaven instance
    is running somewhere other than the default location of localhost:10000,
    then set these environment variables appropriately:
    ```
    set DH_HOST=...
    set DH_PORT=...
    ```

    then run the tests executable:
    ```
    cd /d %DHINSTALL%\bin
    .\dhclient_tests.exe
    ```
