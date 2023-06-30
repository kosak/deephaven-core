# Deephaven Python Client for ticking tables

The Deephaven Python Client enables you to interact with the Deephaven database via Python. The approach we use here is to use Cython to create a thin wrapper around the Deephaven native C++ library.

## Disclaimer

Because this is alpha software, this particular library only addresses the problem of accessing
*ticking* tables from Python. Accessing *static* tables is performed by a different library. We
are working on unifying these two libraries.


## Prerequisites

Clone the Deephaven Core repository. For the remainder of this document we will assume that your
clone is at the location specified by `${DHROOT}`.

## Making the Python venv

To build the code in this directory, you need a python environment with cython and numpy.
In Ubuntu 22.04, the packages `python3`, `python3-dev` and `python3-venv` are required.
The can be installed with

```
sudo apt update
sudo apt -y install python3 python3-dev python3-venv
```

The necessary python venv can be created like so:

```
mkdir ~/py
python3 -m venv ~/py/cython
source ~/py/cython/bin/activate
# From now on your prompt will print '(cython)' in a separate line
# at the end of every command, to remind you you are executing inside
# the venv; to exit the venv just type "deactivate" any time.
#
# Any pip3 installs we do will happen inside the active venv.
pip3 install numpy
pip3 install cython
```

## Building the Deephaven C++ client

The Depehaven python ticking client is built as wrapping of the Deephaven C++ client library
using cython.  We need to build the Deephaven C++ library and its dependencies. To do this, see
the file `${DHROOT}/cpp-client/README.md` in the Deephaven Core github repository.
Note the restrictions on supported platforms mentioned there will for the python ticking client.
The instructions will ask you to select a location for the installation of the C++ client library
and its dependencies.  For the purpose of this document we assume that location is specified in
the `${DHCPP}` environment variable.  On my computer `${DHCPP}` is `$HOME/dhcpp` (where
`$HOME` points to my home directory).

## Building the Deephaven shared library for Python

First, enter the Python client directory:

```
cd ${DHROOT}/py/client-ticking
```

Then run these commands to build the Deephaven shared library:

```
# Ensure the DHCPP environment variable is set per the instructions above
rm -rf build  # Ensure we clean the remnants of any pre-existing build.
CFLAGS="-I${DHCPP}/local/deephaven/include" LDFLAGS="-L${DHCPP}/local/deephaven/lib" python setup.py build_ext -i
```

Once built, a shared object with the binary python module should show up, named like
`pydeephaven.cpython-38-x86_64-linux-gnu.so`.

## Testing the library

Run python from the venv while in this directory, and try this sample Python program:
```
import pydeephaven_ticking as dh
client = dh.Client.connect("localhost:10000")
manager = client.get_manager()
handle = manager.empty_table(10).update(["II= ii"])
print(handle.toString(True))
```
