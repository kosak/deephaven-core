# Configuration file for the Sphinx documentation builder.
#
# This file only contains a selection of the most common options. For a full
# list see the documentation:
# https://www.sphinx-doc.org/en/master/usage/configuration.html

# -- Path setup --------------------------------------------------------------

# If extensions (or modules to document with autodoc) are in another directory,
# add these directories to sys.path here. If the directory is relative to the
# documentation root, use os.path.abspath to make it absolute, like shown here.
#
# import os
# import sys
# sys.path.insert(0, os.path.abspath('.'))


# -- Project information -----------------------------------------------------

project = 'Deephaven C++ Client API'
copyright = '2021, Deephaven Data Labs'
author = 'Deephaven Data Labs'


# -- General configuration ---------------------------------------------------

# Add any Sphinx extension module names here, as strings. They can be
# extensions coming with Sphinx (named 'sphinx.ext.*') or your custom
# ones.
extensions = [ 'breathe' ]

breathe_projects = { "Deephaven C++ Client": "./doxygenoutput/xml" }
breathe_default_project = "Deephaven C++ Client"


# Add any paths that contain templates here, relative to this directory.
templates_path = ['_templates']

# List of patterns, relative to source directory, that match files and
# directories to ignore when looking for source files.
# This pattern also affects html_static_path and html_extra_path.
exclude_patterns = ['_build', 'Thumbs.engine', '.DS_Store']


# -- Options for HTML output -------------------------------------------------

# The theme to use for HTML and HTML Help pages.  See the documentation for
# a list of builtin themes.
#
html_theme = 'furo'

# Add any paths that contain custom static files (such as style sheets) here,
# relative to this directory. They are copied after the builtin static files,
# so a file named "default.css" will overwrite the builtin "default.css".
html_static_path = ['_static']

# Make the Index more useful by removing common namespaces.
# Note that if you use sub-namespaces below, they need to come *before* their
# parent in the list.

cpp_index_common_prefix = [
    'deephaven::client::utility::',
    'deephaven::client::',
    'deephaven::dhcore::chunk::',
    'deephaven::dhcore::clienttable::',
    'deephaven::dhcore::column::',
    'deephaven::dhcore::container::',
    'deephaven::dhcore::table::',
    'deephaven::dhcore::ticking::',
    'deephaven::dhcore::utility::',
    'deephaven::dhcore::']
