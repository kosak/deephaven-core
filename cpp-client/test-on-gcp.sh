#/bin/bash

# REQUIRED CUSTOMIZATIONS

export DEEPHAVEN_VERSION=1.20240517.380   # set to correct version
export DEEPHAVEN_MAJOR_VERSION=20240517   # set to correct version
export DH_TEST_JSON_URL=https://zzzzzz.int.illumon.com:8123/iris/connection.json  # JSON string for your Deephaven server
export DH_TEST_USER=xxxxx  # username for the Deephaven server account
export DH_TEST_PASSWORD=yyyyy  # password for the Deephaven server account

# OPTIONAL CUSTOMIZATIONS

# these are directories on the cloud machine
export SRC_DIR=~/deephaven_src
export INSTALL_DIR=~/deephaven_install
export R_LIBS_USER=~/r_libs_user
export MAKEFLAGS=-j`getconf _NPROCESSORS_ONLN`

# REST OF SCRIPT STARTS HERE
set -euo pipefail

function usage {
    echo "Usage message goes here"
}

function build {
    # Update apt.
    # I was annoyed by the dialog boxes and I tried all kinds of variations
    # (including export DEBIAN_FRONTEND=noninteractive )
    # but nothing seemed to work except for removing the needrestart package
    sudo apt -y remove needrestart
    sudo apt -y update
    sudo apt -y install curl git g++ cmake make build-essential zlib1g-dev libbz2-dev libssl-dev pkg-config
    sudo apt -y install r-base r-recommended

    # Install Docker
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh

    # Make the directories
    mkdir $SRC_DIR $INSTALL_DIR $R_LIBS_USER
    # run Docker hello world (because, why not)
    docker run hello-world
    # Copy Deephaven files and uncompress/untar them
    cd $SRC_DIR
    gsutil cp gs://illumon-software-repo/jenkins/jdk17/release/${DEEPHAVEN_MAJOR_VERSION}/dhe-r-src-${DEEPHAVEN_VERSION}.tgz .
    tar xfz dhe-r-src-${DEEPHAVEN_VERSION}.tgz
    # Hack 1 because of BETA SOFTWARE: fix the directory name
    cd $SRC_DIR
    mv coreplus DhcInDhe
    # Hack 2 because of BETA SOFTWARE: provide missing directory
    cp -r ~/proto-wrappers ${SRC_DIR}/DhcInDhe/cpp-client
    # Build the C++ client. This takes about 20 minutes with this configuration
    cd ${SRC_DIR}/DhcInDhe/cpp-client
    time ./docker-build.sh --prefix ${INSTALL_DIR}
    # Install the C++ client
    cd ${SRC_DIR}/DhcInDhe/cpp-client/build
    tar xfz dhe-cpp-latest-ubuntu-22.04.tgz
    cp -r home/* /home
    # Build the R stuff. This takes about 6 minutes
    cd ${SRC_DIR}/DhcInDhe/R/rdnd
    time ./docker-build.sh --base-distro ubuntu:22.04 --prefix ${INSTALL_DIR}

    cat <<EOF >install_packages.R
    install.packages(c("Rcpp", "R6", "arrow", "dplyr"), Ncpus=parallel::detectCores())  # run this command inside R
    install.packages("build/dhe-r-rdeephaven-latest-ubuntu-22.04.tgz", repos=NULL)
    install.packages("build/dhe-r-rdnd-latest-ubuntu-22.04.tgz", repos=NULL)
    library("rdnd")  # if this looks like it worked, then the above process went well
    q(save="yes")   # exit R
EOF

    time Rscript install_packages.R
}

function test_auth {
    source ${INSTALL_DIR}/env.sh
    cd ${INSTALL_DIR}/src/iris/DhcInDhe/cpp-client/build/tests/auth_tests
    ./auth_tests
}

function test_controller {
    source ${INSTALL_DIR}/env.sh
    cd ${INSTALL_DIR}/src/iris/DhcInDhe/cpp-client/build/tests/controller_tests
    ./controller_tests
}

function test_r {
    source ${INSTALL_DIR}/env.sh
    cd ${INSTALL_DIR}/src/iris/DhcInDhe/cpp-client/build/tests/controller_tests
    ./controller_tests
}

if [ "$#" -ne 1 ]; then
    usage
    exit 1
elif [ "$1" = "build" ]; then
    build
elif [ "$1" = "test_auth" ]; then
    test_auth
elif [ "$1" = "test_controller" ]; then
    test_controller
elif [ "$1" = "test_r" ]; then
    test_r
else
    echo Unexpected argument "$1"
    usage
fi
