# Building the Deephaven clients from source on Windows 10 / 11

These instructions describe how to build the Deephaven clients from source on Windows 10 / 11.

To begin, first read the Prerequisites section. Then follow the instructions for the client you
want to build. Note that some clients depend on other clients being built first. These
dependencies will be explained in the section for each client.

## Prerequisites

### Build machine specifications

* Disk space: at least 150G
* Intel/AMD CPUs (this is our only tested configuration)
* Cores: 2 or 4 cores is fine, *except* for the initial C++ build (without a vcpkg cache).
  If you are doing a C++ build for the first time on a fresh machine, 16 cores is preferable
  in order to populate the vcpkg cache. 

### Software prerequisites

1. Install Visual Studio 2022 Community Edition (or Professional, or Enterprise)
   from here:

   https://visualstudio.microsoft.com/downloads/

   When the installer runs, select the following workloads:
   * "Desktop development with C++"
   * "Python development"

2. Use your preferred version of git, or install Git from here:

   https://git-scm.com/download/win

   When running Setup, select the option "Git from the command line and also
   from 3rd-party software". This allows you to use git from the Windows command
   prompt.

## Dependency Matrix

Some of the clients require others to be built first. This is the client dependency matrix.


| Client (Deephaven version)  | Depends On                |
|-----------------------------|---------------------------|
| C++ (Core)                  | ---                       |
| C++ (Core+)                 | C++ (Core)                |
| python (Core) [non-ticking] | ---                       |
| python-ticking (Core)       | C++ (Core), python (Core) |
| R (Core+)                   | C++ (Core), C++ (Core+)   |
