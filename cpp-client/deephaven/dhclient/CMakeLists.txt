cmake_minimum_required(VERSION 3.14)
project(dhclient)

set(CMAKE_CXX_STANDARD 17)

# for CMAKE_INSTALL_{dir}
include(GNUInstallDirs)

find_package(Arrow CONFIG REQUIRED)
find_package(ArrowFlight CONFIG REQUIRED HINTS ${Arrow_DIR})
find_package(Immer CONFIG REQUIRED)
find_package(Protobuf CONFIG REQUIRED)
find_package(gRPC CONFIG REQUIRED)
find_package(Threads REQUIRED)

set(ALL_FILES
    src/server/server.cc
    include/private/deephaven/client/server/server.h

    src/impl/aggregate_impl.cc
    src/impl/client_impl.cc
    src/impl/table_handle_impl.cc
    src/impl/table_handle_manager_impl.cc
    src/impl/update_by_operation_impl.cc
    include/private/deephaven/client/impl/aggregate_impl.h
    include/private/deephaven/client/impl/client_impl.h
    include/private/deephaven/client/impl/table_handle_impl.h
    include/private/deephaven/client/impl/table_handle_manager_impl.h
    include/private/deephaven/client/impl/update_by_operation_impl.h
    include/private/deephaven/client/impl/util.h

    src/arrowutil/arrow_client_table.cc
    include/private/deephaven/client/arrowutil/arrow_client_table.h
    include/private/deephaven/client/arrowutil/arrow_column_source.h
    include/private/deephaven/client/arrowutil/arrow_value_converter.h
    include/private/deephaven/client/arrowutil/arrow_visitors.h

    src/client_options.cc
    src/client.cc
    src/flight.cc
    src/update_by.cc
    include/public/deephaven/client/client.h
    include/public/deephaven/client/client_options.h
    include/public/deephaven/client/flight.h
    include/public/deephaven/client/update_by.h

    src/interop/client_interop.cc
    src/interop/client_options_interop.cc
    src/interop/update_by_interop.cc
    include/public/deephaven/client/interop/client_interop.h
    include/public/deephaven/client/interop/client_options_interop.h
    include/public/deephaven/client/interop/update_by_interop.h

    src/subscription/subscribe_thread.cc

    include/private/deephaven/client/subscription/subscribe_thread.h
    include/private/deephaven/client/subscription/subscription_handle.h

    src/utility/executor.cc
    include/private/deephaven/client/utility/executor.h

    src/utility/arrow_util.cc
    src/utility/table_maker.cc

    include/public/deephaven/client/utility/arrow_util.h
    include/public/deephaven/client/utility/misc_types.h
    include/public/deephaven/client/utility/table_maker.h

    proto/deephaven/proto/application.grpc.pb.cc
    proto/deephaven/proto/application.grpc.pb.h
    proto/deephaven/proto/application.pb.cc
    proto/deephaven/proto/application.pb.h
    proto/deephaven/proto/config.grpc.pb.cc
    proto/deephaven/proto/config.grpc.pb.h
    proto/deephaven/proto/config.pb.cc
    proto/deephaven/proto/config.pb.h
    proto/deephaven/proto/console.grpc.pb.cc
    proto/deephaven/proto/console.grpc.pb.h
    proto/deephaven/proto/console.pb.cc
    proto/deephaven/proto/console.pb.h
    proto/deephaven/proto/inputtable.grpc.pb.cc
    proto/deephaven/proto/inputtable.grpc.pb.h
    proto/deephaven/proto/inputtable.pb.cc
    proto/deephaven/proto/inputtable.pb.h
    proto/deephaven/proto/object.grpc.pb.cc
    proto/deephaven/proto/object.grpc.pb.h
    proto/deephaven/proto/object.pb.cc
    proto/deephaven/proto/object.pb.h
    proto/deephaven/proto/partitionedtable.grpc.pb.cc
    proto/deephaven/proto/partitionedtable.grpc.pb.h
    proto/deephaven/proto/partitionedtable.pb.cc
    proto/deephaven/proto/partitionedtable.pb.h
    proto/deephaven/proto/session.grpc.pb.cc
    proto/deephaven/proto/session.grpc.pb.h
    proto/deephaven/proto/session.pb.cc
    proto/deephaven/proto/session.pb.h
    proto/deephaven/proto/table.grpc.pb.cc
    proto/deephaven/proto/table.grpc.pb.h
    proto/deephaven/proto/table.pb.cc
    proto/deephaven/proto/table.pb.h
    proto/deephaven/proto/ticket.grpc.pb.cc
    proto/deephaven/proto/ticket.grpc.pb.h
    proto/deephaven/proto/ticket.pb.cc
    proto/deephaven/proto/ticket.pb.h
)

add_library(dhclient SHARED ${ALL_FILES})

# This is so deephaven::client works both when using the installed CMake config
# and when using this project as a CMake subdirectory of your own project.
add_library(deephaven::client ALIAS dhclient)

set_property(TARGET dhclient PROPERTY POSITION_INDEPENDENT_CODE ON)

if (LINUX)
    target_compile_options(dhclient PRIVATE -Wall -Werror -Wno-deprecated-declarations)
endif()

if (WIN32)
    set_property(TARGET dhclient PROPERTY WINDOWS_EXPORT_ALL_SYMBOLS ON)
    # /Wall is a bit too chatty so we stick with /W3
    # /bigobj needed because ticking/immer_table_state.cc compiles to something too large apparently
    target_compile_options(dhclient PRIVATE /W3 /bigobj)
endif()

target_include_directories(dhclient PRIVATE include/private)
target_include_directories(dhclient PUBLIC $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include/public>)
# Protos and flatbuf are doing their own thing.
target_include_directories(dhclient PRIVATE "./proto")
target_include_directories(dhclient PRIVATE "./flatbuf")

target_link_libraries(dhclient PUBLIC deephaven::dhcore)

target_link_libraries(dhclient PUBLIC ArrowFlight::arrow_flight_shared)
target_link_libraries(dhclient PUBLIC Arrow::arrow_shared)
target_link_libraries(dhclient PRIVATE protobuf::libprotobuf)
target_link_libraries(dhclient PRIVATE gRPC::grpc++)
target_link_libraries(dhclient PRIVATE Threads::Threads)
