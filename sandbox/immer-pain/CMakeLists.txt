cmake_minimum_required(VERSION 3.26)
project(immerpain)

set(CMAKE_CXX_STANDARD 17)

add_executable(immerpain main.cpp)

target_include_directories(immerpain PUBLIC .)

target_compile_options(immerpain PRIVATE -Wall -Werror)
