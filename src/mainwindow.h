#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>

class QTabWidget;
class QPoint;

class MainWindow final : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);

private:
    void addGroup();
    void closeGroup(int index);
    void handleGroupDoubleClick(int index);
    void renameGroup(int index);
    void showGroupContextMenu(const QPoint &position);

    QTabWidget *m_groups;
    int m_nextGroupNumber = 1;
};

#endif
