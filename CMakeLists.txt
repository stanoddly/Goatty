cmake_minimum_required(VERSION 3.18)

project(Goatty VERSION 0.1.0 LANGUAGES CXX)

include(GNUInstallDirs)

find_package(Qt6 6.6 REQUIRED COMPONENTS Widgets)
find_package(QTermWidget6 2.4 REQUIRED)

qt_standard_project_setup()

add_executable(goatty src/main.cpp src/mainwindow.cpp src/mainwindow.h src/terminalgroup.cpp src/terminalgroup.h)

target_compile_features(goatty PRIVATE cxx_std_17)
target_link_libraries(goatty PRIVATE Qt6::Widgets qtermwidget6)

install(TARGETS goatty RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR})
