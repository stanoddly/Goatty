#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QPointer>

class QEvent;
class QObject;
class QTabWidget;
class QPoint;
class TerminalGroup;

class MainWindow final : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = nullptr);

protected:
    bool eventFilter(QObject *watched, QEvent *event) override;

private:
    void addGroup();
    void closeGroup(int index);
    void handleGroupDoubleClick(int index);
    bool previewDraggedTerminalIn(TerminalGroup *destination);
    void renameGroup(int index);
    void restoreDraggedTerminal();
    void showGroupContextMenu(const QPoint &position);

    QTabWidget *m_groups;
    QPointer<TerminalGroup> m_terminalDragSource;
    QPointer<TerminalGroup> m_terminalDragCurrentGroup;
    int m_nextGroupNumber = 1;
};

#endif
