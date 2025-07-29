if not defined DHSRC (
  set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
)

python3 -m venv %HOMEDRIVE%%HOMEPATH%\cython || exit /b

call "%HOMEDRIVE%%HOMEPATH%\cython\Scripts\activate" || exit /b

pip3 install cython wheel || exit /b

cd /d %DHSRC%\deephaven-core\py\client || exit /b

pip3 install -r requirements-dev.txt || exit /b

FOR /F "tokens=*" %%a IN ('..\..\gradlew :printVersion -q') DO (
  SET DEEPHAVEN_VERSION=%%a
)

if not defined DEEPHAVEN_VERSION (
  echo DEEPHAVEN_VERSION is not defined
  exit /b 1
)

python setup.py bdist_wheel || exit /b

FOR %%f IN (".\dist\*.whl") DO (
  SET DEEPHAVEN_WHEEL_FILE=%%f
)

pip3 install --force --no-deps %DEEPHAVEN_WHEEL_FILE% || exit /b
