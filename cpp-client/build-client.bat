REM Performance note
REM With an unpopulated vcpkg cache, this script takes a long time to build
REM the dependent packages (could take hours on a slow machine).
REM Once the vcpkg cache is populated, it is fast enough (maybe 5-10 minutes).
REM If this machine is running in the cloud, one reasonable strategy is to
REM first configure a machine with a large number of cores (16+) and run this
REM script once to populate the vcpkg cache. Assuming the vcpkg persists
REM between runs, subsequent runs can work with a smaller number of cores.

if not defined DHSRC (
  set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
)
if not defined DHINSTALL (
  set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
)
set VCPKG_ROOT=%DHSRC%\vcpkg
echo *** MAKING DIRECTORIES
mkdir %DHSRC% || exit /b
mkdir %DHINSTALL% || exit /b

echo *** CLONING REPOSITORIES ***
cd /d %DHSRC% || exit /b
git clone https://github.com/microsoft/vcpkg.git || exit /b
git clone https://github.com/deephaven/deephaven-core.git || exit /b

echo *** BOOTSTRAPPING VCPKG ***
cd /d %VCPKG_ROOT% || exit /b
call .\bootstrap-vcpkg.bat || exit /b

echo *** BUILDING DEPENDENT LIBRARIES ***
cd /d %DHSRC%\deephaven-core\cpp-client\deephaven || exit /b
%VCPKG_ROOT%\vcpkg.exe install --triplet x64-windows || exit /b

echo *** CONFIGURING DEEPHAVEN BUILD ***
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%/scripts/buildsystems/vcpkg.cmake -DCMAKE_INSTALL_PREFIX=%DHINSTALL% -DX_VCPKG_APPLOCAL_DEPS_INSTALL=ON || exit /b

echo *** BUILDING C++ CLIENT ***
cmake --build build --config RelWithDebInfo --target install -- /p:CL_MPCount=16 -m:1 || exit /b
