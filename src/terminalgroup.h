#ifndef TERMINALGROUP_H
#define TERMINALGROUP_H

#include <QHash>
#include <QWidget>

class QTabWidget;
class QTermWidget;
class QPoint;

class TerminalGroup final : public QWidget
{
    Q_OBJECT

public:
    explicit TerminalGroup(QWidget *parent = nullptr);

    void addTerminal();
    void closeCurrentTerminal();
    void selectNextTerminal();
    void selectPreviousTerminal();
    void focusCurrentTerminal();

signals:
    void emptied();
    void newGroupRequested();
    void renameGroupRequested();
    void closeGroupRequested();

private slots:
    void terminalFinished();
    void terminalStateChanged();

private:
    void closeTerminal(int index);
    void renameTerminal(int index);
    void resetTerminalTitle(int index);
    void showTerminalContextMenu(const QPoint &position);
    void updateTerminalTitle(QTermWidget *terminal);

    QTabWidget *m_terminals;
    QHash<QTermWidget *, QString> m_terminalNames;
    int m_nextTerminalNumber = 1;
};

#endif
