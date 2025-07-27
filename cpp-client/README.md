# Building the C++ client on Ubuntu 20.04 / 22.04 and Windows 10 / 11.

## Introduction

This document provides instructions to build the Deephaven C++ client from source
on Ubuntu and Windows. The Ubuntu instructions are in the first part of the document;
the Windows instructions are in the second part of the document.

## Before you start

To actually use Deephaven, for example running these examples and unit
tests, you will eventually need to have a server running. If you have
an existing server running Deephaven Core, you should be able to
connect to that. However, if you don't have one, you can follow the
instructions [here](https://deephaven.io/core/docs/getting-started/launch-build/).

Note that although clients can run on Linux or Windows, Deephaven servers can
currently run only on Linux.

You can build and install client libraries, tests, and examples
without having a server installed. However, you will eventually need to
connect to a server when you want to run them.

## Building the C++ client on Ubuntu 22.04

We have tested these instructions in Ubuntu 22.04 with the default
C++ compiler and tool suite (cmake etc.). We have used the instructions in the past to build
for older Ubuntu versions (20.04) and for some Fedora versions, but we don't regularly test
on them anymore so we do not guarantee they are current for those platforms.


1. Start with an Ubuntu 22.04 install

2. Establish source and installation directories. You can use different values than the ones
   we have chosen below.

   ```
   export DHSRC=$HOME/src/deephaven
   export DHCPP=$HOME/dhcpp
   mkdir -p $DHSRC
   mkdir -p $DHCPP
   ```


3. Get build tools
   ```
   sudo apt update
   sudo apt install curl git g++ cmake make build-essential zlib1g-dev libbz2-dev libssl-dev pkg-config
   ```

   See the notes at the end of this document if you need the equivalent packages for Fedora.

4. Clone deephaven-core sources.
   ```
   cd $DHSRC
   git clone https://github.com/deephaven/deephaven-core.git
   ```

5. Build and install dependencies for the Deephaven C++ client. Copy the install script
   to your installation directory and run it there.

   ```
   cp $DHSRC/deephaven-core/cpp-client/build-dependencies.sh $DHCPP
   cd $DHCPP
   ./build-dependencies.sh 2>&1 | tee build-dependencies.log
   ```

6. Build and install the Deephaven C++ client.

   ```
   source $DHCPP/env.sh
   cd $DHSRC/deephaven-core/cpp-client/deephaven/
   cmake -S . -B build \
       -DCMAKE_INSTALL_LIBDIR=lib \
       -DCMAKE_CXX_STANDARD=17 \
       -DCMAKE_INSTALL_PREFIX=${DHCPP} \
       -DCMAKE_BUILD_TYPE=RelWithDebInfo \
       -DBUILD_SHARED_LIBS=ON \
     && \
   VERBOSE=1 cmake --build build --target install -- -j$NCPUS
   ```

7. (Optional) run a smoke test.
   Note this assumes a Deephaven Core server is running.

   ```
   export DH_HOST=... # your server host address goes here
   export DH_PORT=... # your server port goes here
   cd $DHSRC/deephaven-core/cpp-client/deephaven/build/examples
   cd hello_world
   ./hello_world
   ```

8. (Optional) run the unit tests.
   Note this assumes a Deephaven Core server is running.

   ```
   export DH_HOST=... # your server host address goes here
   export DH_PORT=... # your server port goes here
   cd $DHSRC/deephaven-core/cpp-client/deephaven/build/tests
   make -j$NCPUS
   ./tests
   ```

### Building in different distributions or with older toolchains.

   While we don't support other linux distributions or GCC versions earlier
   than 11, this section provides some notes that may help you
   in that situation.

   * GCC 8 mixed with older versions of GNU as/binutils may fail to compile
     `roaring.c` with an error similar to:
     ```
     /tmp/cczCvQKd.s: Assembler messages:
     /tmp/cczCvQKd.s:45826: Error: no such instruction: `vpcompressb %zmm0,%zmm1{%k2}'
     /tmp/cczCvQKd.s:46092: Error: no such instruction: `vpcompressb %zmm0,%zmm1{%k1}'
     ```
     In that case, add `-DCMAKE_C_FLAGS=-DCROARING_COMPILER_SUPPORTS_AVX512=0`
     to the list of arguments to `cmake`.

   * Some platforms combining old versions of GCC and cmake may fail
     to set the cmake C++ standard to 17 without explicitly adding
     `-DCMAKE_CXX_STANDARD=17` to the list of arguments to `cmake`.
     Note the default mode for C++ is `-std=gnu++17` for GCC 11.

Notes
  (1) The standard assumptions for `Debug` and `Release` apply here.
      With a `Debug` build you get debug information which is useful during
      development and testing of your own code that depends on the client
      and indirectly on these libraries.  A `Release` build gives you
      optimized libraries that are faster and smaller but with no
      debugging information.  Note that while in general it is expected
      to be able to freely mix some `Debug` and `Release` code,
      some of the dependent libraries are incompatible; in particular,
      protobuf generates different code and code compiled for a `Release`
      target using protobuf header files will not link against a `Debug`
      version of protobuf.  To keep things simple, we suggest that you run
      a consistent setting for your code and all dependencies.
  (2) In Fedora, the packages needed for building:

      ```
      dnf -y groupinstall 'Development Tools'
      dnf -y install curl cmake gcc-c++ openssl-devel libcurl-devel
      ```

### Updating proto generated C++ stubs (intended for developers)
   1. Ensure you have a local installation of the dependent libraries
      as described earlier in this document.  Source the `env.sh`
      file to ensure you have the correct environment variable definitions
      in your shell for the steps below.

   2. In the `proto/proto-backplane-grpc/src/main/proto` directory
      (relative from your deephave-core clone base directory),
      run the `build-cpp-protos.sh` script.
      This should generate up-to-date versions of the C++ stubs
      according to the proto sources on the same clone.

## Building the C++ client on Windows 10 / Windows 11, Pro and Enterprise.

These instructions have been tested on both Windows 10 and Windows 11.

### Build machine specifications

* Disk space: at least 150G
* Cores: prefer 16 or more for the first-time build. Subsequent builds can be done with fewer cores.

### Prerequisites

1. Install Visual Studio 2022 Community Edition (or Professional, or Enterprise)
   from here:

   https://visualstudio.microsoft.com/downloads/

   When the installer runs, select the workload "Desktop development with C++"

2. Use your preferred version of git, or install Git from here:

   https://git-scm.com/download/win

   When running Setup, select the option "Git from the command line and also
   from 3rd-party software". This allows you to use git from the Windows command
   prompt.

3. Configure your computer to allow Windows long pathname support. The
   Deephaven git repository currently has some paths longer than the Windows
   maximum path length. To work around this, you will need to change a git
   configuration setting and a Windows group policy setting.

   3.1 git configuration setting:
   * Open Git Bash as an administrator.
   * Run the command `git config --system core.longpaths true`

   3.2 group policy setting
   * For Windows Pro or Enterprise editions, open the Group Policy Editor (gpedit.msc) and go to
     `Computer Configuration > Administrative Templates > System > Filesystem`. Then enable the
     `Enable Win32 long paths` setting.
   * For Windows Home editions (not tested), use regedit to modify the registry key
     `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem` and set the DWORD value
      for `LongPathsEnabled` to 1.

### Building the C++ client

1. We will do the actual build process inside a Visual Studio developer
   command prompt. Run the developer command prompt by navigating here:

   `Start -> V -> Visual Studio 2022 -> Developer Command Prompt for VS 2022`

2. Establish source and installation directories. You can use different values than the ones
   we have chosen below.
   ```
   set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
   set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
   set VCPKG_ROOT=%DHSRC%\vcpkg
   mkdir %DHSRC%
   mkdir %DHINSTALL%
   ```

3. Clone the Deephaven Core repository and the vcpkg repository.

   ```
   cd /d %DHSRC%
   git clone https://github.com/microsoft/vcpkg.git
   git clone https://github.com/deephaven/deephaven-core.git
   ```

4. Do the one-time installation steps for vcpkg.
   ```
   cd /d %VCPKG_ROOT%
   .\bootstrap-vcpkg.bat
   ```

5. Build and install the dependent packages. On my computer (a relatively fast
   CPU with 16+ cores) this process took about 20 minutes.
   ```
   cd /d %DHSRC%\deephaven-core\cpp-client\deephaven
   %VCPKG_ROOT%\vcpkg.exe install --triplet x64-windows
    ```

6. Configure the build for Deephaven Core:
   ``` 
   cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%/scripts/buildsystems/vcpkg.cmake -DCMAKE_INSTALL_PREFIX=%DHINSTALL% -DX_VCPKG_APPLOCAL_DEPS_INSTALL=ON
   ```
   
9. Finally, build and install Deephaven Core.
   ```
   # Replace '16' by the number of CPU threads you want to use for building
   cmake --build build --config RelWithDebInfo --target install -- /p:CL_MPCount=16 -m:1
   ```

10. (Optional) run the tests.
    Note this assumes a Deephaven Core server is running.

    ```
    export DH_HOST=... # your server host address goes here
    export DH_PORT=... # your server port goes here
    cd /d %DHINSTALL%\bin
    .\dhclient_tests.exe
    ```
