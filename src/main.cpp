#include <QApplication>
#include <QString>

#include "mainwindow.h"

int main(int argc, char *argv[])
{
    QApplication application(argc, argv);
    QApplication::setApplicationName(QStringLiteral("goatty"));
    QApplication::setApplicationDisplayName(QStringLiteral("Goatty"));
    QApplication::setApplicationVersion(QStringLiteral("0.1.0"));

    MainWindow window;
    window.show();

    return application.exec();
}
