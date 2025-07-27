set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
set VCPKG_ROOT=%DHSRC%\vcpkg
echo *** MAKING DIRECTORIES
mkdir %DHSRC% || exit /b
mkdir %DHINSTALL% || exit /b

echo *** CLONING REPOSITORIES ***
cd /d %DHSRC% || exit /b
git clone https://github.com/microsoft/vcpkg.git || exit /b

echo *** WARNING FIX THIS REPOSITORY ***
echo *** WARNING FIX THIS REPOSITORY ***
echo *** WARNING FIX THIS REPOSITORY ***
git clone -b kosak_todo-fixes https://github.com/kosak/deephaven-core.git || exit /b

echo *** BOOTSTRAPPING VCPKG ***
cd /d %VCPKG_ROOT% || exit /b
call .\bootstrap-vcpkg.bat || exit /b

echo *** BUILDING DEPENDENT LIBRARIES ***
cd /d %DHSRC%\deephaven-core\cpp-client\deephaven || exit /b
%VCPKG_ROOT%\vcpkg.exe install --triplet x64-windows || exit /b

echo *** CONFIGURING DEEPHAVEN BUILD ***
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%/scripts/buildsystems/vcpkg.cmake -DCMAKE_INSTALL_PREFIX=%DHINSTALL% -DX_VCPKG_APPLOCAL_DEPS_INSTALL=ON || exit /b

ecoh *** BUILDING C++ CLIENT ***
cmake --build build --config RelWithDebInfo --target install -- /p:CL_MPCount=16 -m:1 || exit /b
