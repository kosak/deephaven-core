if not defined DHSRC (
  set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
)

if not defined DHINSTALL (
  set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
)

cd /d %DHSRC%\deephaven-core || exit /b

FOR /F "tokens=*" %%a IN ('.\gradlew :printVersion -q') DO (
  SET DEEPHAVEN_VERSION=%%a
)

if not defined DEEPHAVEN_VERSION (
  echo DEEPHAVEN_VERSION is not defined
  exit /b
)

cd %DHSRC%\deephaven-core\py\client-ticking
python setup.py build_ext -i || exit /b

python setup.py bdist_wheel || exit /b

FOR %%f IN (".\dist\*.whl") DO (
  SET DEEPHAVEN_WHEEL_FILE=%%f
)

pip3 install --force --no-deps %DEEPHAVEN_WHEEL_FILE% || exit /b
