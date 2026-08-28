#ifndef TERMINALGROUP_H
#define TERMINALGROUP_H

#include <QHash>
#include <QPoint>
#include <QPointer>
#include <QWidget>

class QEvent;
class QObject;
class QTabWidget;
class QTermWidget;
class QTimer;

class TerminalGroup final : public QWidget
{
    Q_OBJECT

public:
    inline static constexpr char TerminalDragMimeType[] = "application/x-goatty-terminal-tab";

    explicit TerminalGroup(QWidget *parent = nullptr);

    void addTerminal();
    void closeCurrentTerminal();
    void selectNextTerminal();
    void selectPreviousTerminal();
    void focusCurrentTerminal();
    bool moveDraggedTerminalTo(TerminalGroup *destination);

signals:
    void emptied();
    void newGroupRequested();
    void renameGroupRequested();
    void closeGroupRequested();

private slots:
    void terminalFinished();
    void terminalStateChanged();

protected:
    bool eventFilter(QObject *watched, QEvent *event) override;

private:
    void closeTerminal(int index);
    void monitorTerminal(QTermWidget *terminal, QTimer *titleTimer);
    void renameTerminal(int index);
    void resetTerminalTitle(int index);
    void showTerminalContextMenu(const QPoint &position);
    void updateTerminalTitle(QTermWidget *terminal);

    QTabWidget *m_terminals;
    QHash<QTermWidget *, QString> m_terminalNames;
    QHash<QTermWidget *, QTimer *> m_titleTimers;
    QPointer<QWidget> m_draggedTerminal;
    QPoint m_dragStartPosition;
    int m_dragOriginalIndex = -1;
    bool m_dragInProgress = false;
    int m_nextTerminalNumber = 1;
};

#endif
