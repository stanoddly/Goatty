#include "terminalgroup.h"

#include <QApplication>
#include <QDir>
#include <QDrag>
#include <QEvent>
#include <QFile>
#include <QFileInfo>
#include <QInputDialog>
#include <QLineEdit>
#include <QMenu>
#include <QMimeData>
#include <QMouseEvent>
#include <QString>
#include <QTabBar>
#include <QTabWidget>
#include <QTimer>
#include <QToolButton>
#include <QVBoxLayout>
#include <QWidget>

#include <qtermwidget.h>

namespace
{
QString directoryName(const QString &path)
{
    QString cleanPath = QDir::cleanPath(path);
    if (cleanPath == QDir::homePath())
    {
        return QStringLiteral("~");
    }
    if (cleanPath == QStringLiteral("/"))
    {
        return cleanPath;
    }
    return QFileInfo(cleanPath).fileName();
}

QString processName(int processId)
{
    if (processId <= 0)
    {
        return QString();
    }

    QFile processNameFile(QStringLiteral("/proc/%1/comm").arg(processId));
    if (!processNameFile.open(QIODevice::ReadOnly | QIODevice::Text))
    {
        return QString();
    }
    return QString::fromLocal8Bit(processNameFile.readLine()).trimmed();
}
}

TerminalGroup::TerminalGroup(QWidget *parent)
    : QWidget(parent)
    , m_terminals(new QTabWidget(this))
{
    m_terminals->setDocumentMode(true);
    m_terminals->setMovable(true);
    m_terminals->setTabsClosable(true);
    m_terminals->tabBar()->setExpanding(false);
    m_terminals->tabBar()->setContextMenuPolicy(Qt::CustomContextMenu);
    m_terminals->tabBar()->installEventFilter(this);

    QToolButton *newTerminalButton = new QToolButton(m_terminals);
    newTerminalButton->setText(QStringLiteral("+"));
    newTerminalButton->setToolTip(QStringLiteral("New terminal (Ctrl+T)"));
    newTerminalButton->setAccessibleName(QStringLiteral("New terminal"));
    newTerminalButton->setAutoRaise(true);
    m_terminals->setCornerWidget(newTerminalButton, Qt::TopRightCorner);

    QVBoxLayout *layout = new QVBoxLayout(this);
    layout->setContentsMargins(0, 0, 0, 0);
    layout->setSpacing(0);
    layout->addWidget(m_terminals);

    connect(newTerminalButton, &QToolButton::clicked, this, &TerminalGroup::addTerminal);
    connect(m_terminals, &QTabWidget::tabCloseRequested, this, &TerminalGroup::closeTerminal);
    connect(m_terminals->tabBar(), &QTabBar::customContextMenuRequested, this, &TerminalGroup::showTerminalContextMenu);
    connect(m_terminals, &QTabWidget::tabBarDoubleClicked, this, [this](int index) {
        if (index < 0)
        {
            addTerminal();
        }
        else
        {
            renameTerminal(index);
        }
    });
    connect(m_terminals, &QTabWidget::currentChanged, this, [this](int) {
        focusCurrentTerminal();
    });

    addTerminal();
}

void TerminalGroup::addTerminal()
{
    QTermWidget *terminal = new QTermWidget(0);
    terminal->setAutoClose(true);
    terminal->setColorScheme(QStringLiteral("Nord"));
    terminal->setWorkingDirectory(QDir::currentPath());

    QString shell = qEnvironmentVariable("SHELL").trimmed();
    if (!shell.isEmpty())
    {
        terminal->setShellProgram(shell);
    }

    int index = m_terminals->addTab(terminal, QStringLiteral("Shell %1").arg(m_nextTerminalNumber++));
    m_terminals->setCurrentIndex(index);
    QTimer *titleTimer = new QTimer(terminal);
    titleTimer->setInterval(1000);
    monitorTerminal(terminal, titleTimer);
    titleTimer->start();

    QTimer::singleShot(0, terminal, [this, terminal]() {
        terminal->startShellProgram();
        terminal->setFocus();
        updateTerminalTitle(terminal);
    });
}

bool TerminalGroup::moveDraggedTerminalTo(TerminalGroup *destination)
{
    QTermWidget *terminal = qobject_cast<QTermWidget *>(m_draggedTerminal.data());
    if (terminal == nullptr || destination == nullptr || destination == this)
    {
        return false;
    }

    int index = m_terminals->indexOf(terminal);
    if (index < 0)
    {
        return false;
    }

    QTimer *titleTimer = m_titleTimers.take(terminal);
    if (titleTimer == nullptr)
    {
        return false;
    }

    QString title = m_terminals->tabText(index);
    bool hasManualTitle = m_terminalNames.contains(terminal);
    QString manualTitle = m_terminalNames.take(terminal);
    QObject::disconnect(terminal, nullptr, this, nullptr);
    QObject::disconnect(titleTimer, nullptr, this, nullptr);
    m_terminals->removeTab(index);

    int destinationIndex = destination->m_terminals->addTab(terminal, title);
    if (hasManualTitle)
    {
        destination->m_terminalNames.insert(terminal, manualTitle);
    }
    destination->monitorTerminal(terminal, titleTimer);
    destination->m_terminals->setCurrentIndex(destinationIndex);
    if (destination->m_dragInProgress && destination->m_dragOriginalIndex >= 0 && destination->m_dragOriginalIndex < destination->m_terminals->count())
    {
        destination->m_terminals->tabBar()->moveTab(destinationIndex, destination->m_dragOriginalIndex);
    }
    m_draggedTerminal = nullptr;
    destination->m_draggedTerminal = terminal;

    if (m_terminals->count() == 0 && !m_dragInProgress)
    {
        emit emptied();
    }

    return true;
}

void TerminalGroup::closeCurrentTerminal()
{
    closeTerminal(m_terminals->currentIndex());
}

void TerminalGroup::selectNextTerminal()
{
    if (m_terminals->count() > 1)
    {
        m_terminals->setCurrentIndex((m_terminals->currentIndex() + 1) % m_terminals->count());
    }
}

void TerminalGroup::selectPreviousTerminal()
{
    if (m_terminals->count() > 1)
    {
        m_terminals->setCurrentIndex((m_terminals->currentIndex() + m_terminals->count() - 1) % m_terminals->count());
    }
}

void TerminalGroup::focusCurrentTerminal()
{
    QWidget *terminal = m_terminals->currentWidget();
    if (terminal != nullptr)
    {
        terminal->setFocus();
    }
}

bool TerminalGroup::eventFilter(QObject *watched, QEvent *event)
{
    if (watched != m_terminals->tabBar())
    {
        return QWidget::eventFilter(watched, event);
    }

    if (event->type() == QEvent::MouseButtonPress)
    {
        QMouseEvent *mouseEvent = static_cast<QMouseEvent *>(event);
        if (mouseEvent->button() == Qt::LeftButton)
        {
            int index = m_terminals->tabBar()->tabAt(mouseEvent->position().toPoint());
            m_draggedTerminal = m_terminals->widget(index);
            m_dragStartPosition = mouseEvent->position().toPoint();
            m_dragOriginalIndex = index;
        }
    }
    else if (event->type() == QEvent::MouseButtonRelease)
    {
        m_draggedTerminal = nullptr;
        m_dragOriginalIndex = -1;
    }
    else if (event->type() == QEvent::MouseMove)
    {
        QMouseEvent *mouseEvent = static_cast<QMouseEvent *>(event);
        if ((mouseEvent->buttons() & Qt::LeftButton) == 0)
        {
            m_draggedTerminal = nullptr;
            m_dragOriginalIndex = -1;
        }
        else if (m_draggedTerminal != nullptr && (mouseEvent->position().toPoint() - m_dragStartPosition).manhattanLength() >= QApplication::startDragDistance()
            && !m_terminals->tabBar()->rect().contains(mouseEvent->position().toPoint()))
        {
            int index = m_terminals->indexOf(m_draggedTerminal);
            if (index < 0)
            {
                m_draggedTerminal = nullptr;
                return true;
            }

            QPointer<QWidget> draggedTerminal = m_draggedTerminal;
            int originalIndex = m_dragOriginalIndex;
            QMouseEvent releaseEvent(QEvent::MouseButtonRelease, mouseEvent->position(), mouseEvent->globalPosition(), Qt::LeftButton, Qt::NoButton, mouseEvent->modifiers());
            QApplication::sendEvent(m_terminals->tabBar(), &releaseEvent);
            m_draggedTerminal = draggedTerminal;
            m_dragOriginalIndex = originalIndex;

            QDrag drag(this);
            QMimeData *mimeData = new QMimeData;
            mimeData->setData(QString::fromLatin1(TerminalDragMimeType), QByteArray());
            drag.setMimeData(mimeData);
            m_dragInProgress = true;
            Qt::DropAction dropAction = drag.exec(Qt::MoveAction);
            m_dragInProgress = false;
            if (dropAction != Qt::MoveAction && draggedTerminal != nullptr)
            {
                int currentIndex = m_terminals->indexOf(draggedTerminal);
                if (currentIndex >= 0 && originalIndex >= 0 && originalIndex < m_terminals->count() && currentIndex != originalIndex)
                {
                    m_terminals->tabBar()->moveTab(currentIndex, originalIndex);
                }
            }
            m_draggedTerminal = nullptr;
            m_dragOriginalIndex = -1;
            if (m_terminals->count() == 0)
            {
                emit emptied();
            }
            return true;
        }
    }

    return QWidget::eventFilter(watched, event);
}

void TerminalGroup::closeTerminal(int index)
{
    QWidget *terminal = m_terminals->widget(index);
    if (terminal == nullptr)
    {
        return;
    }

    m_terminals->removeTab(index);
    m_terminalNames.remove(qobject_cast<QTermWidget *>(terminal));
    m_titleTimers.remove(qobject_cast<QTermWidget *>(terminal));
    terminal->deleteLater();

    if (m_terminals->count() == 0 && !m_dragInProgress)
    {
        emit emptied();
    }
}

void TerminalGroup::monitorTerminal(QTermWidget *terminal, QTimer *titleTimer)
{
    connect(terminal, SIGNAL(finished()), this, SLOT(terminalFinished()));
    connect(terminal, SIGNAL(titleChanged()), this, SLOT(terminalStateChanged()));
    connect(terminal, SIGNAL(currentDirectoryChanged(QString)), this, SLOT(terminalStateChanged()));
    connect(titleTimer, &QTimer::timeout, this, [this, terminal]() {
        updateTerminalTitle(terminal);
    });
    m_titleTimers.insert(terminal, titleTimer);
}

void TerminalGroup::renameTerminal(int index)
{
    QTermWidget *terminal = qobject_cast<QTermWidget *>(m_terminals->widget(index));
    if (terminal == nullptr)
    {
        return;
    }

    bool accepted = false;
    QString title = QInputDialog::getText(this, QStringLiteral("Rename terminal"), QStringLiteral("Terminal name:"), QLineEdit::Normal, m_terminals->tabText(index), &accepted).trimmed();
    if (accepted && !title.isEmpty())
    {
        m_terminalNames.insert(terminal, title);
        m_terminals->setTabText(index, title);
    }
}

void TerminalGroup::resetTerminalTitle(int index)
{
    QTermWidget *terminal = qobject_cast<QTermWidget *>(m_terminals->widget(index));
    if (terminal != nullptr)
    {
        m_terminalNames.remove(terminal);
        updateTerminalTitle(terminal);
    }
}

void TerminalGroup::terminalFinished()
{
    QTermWidget *terminal = qobject_cast<QTermWidget *>(sender());
    if (terminal != nullptr)
    {
        closeTerminal(m_terminals->indexOf(terminal));
    }
}

void TerminalGroup::terminalStateChanged()
{
    QTermWidget *terminal = qobject_cast<QTermWidget *>(sender());
    if (terminal != nullptr)
    {
        updateTerminalTitle(terminal);
    }
}

void TerminalGroup::showTerminalContextMenu(const QPoint &position)
{
    int index = m_terminals->tabBar()->tabAt(position);
    if (index >= 0)
    {
        m_terminals->setCurrentIndex(index);
    }

    QMenu menu(this);
    QAction *newTerminalAction = menu.addAction(QStringLiteral("New terminal"));
    QAction *newGroupAction = menu.addAction(QStringLiteral("New group"));
    menu.addSeparator();
    QAction *renameTerminalAction = nullptr;
    QAction *resetTerminalTitleAction = nullptr;
    if (index >= 0)
    {
        renameTerminalAction = menu.addAction(QStringLiteral("Rename terminal"));
        resetTerminalTitleAction = menu.addAction(QStringLiteral("Reset terminal name"));
        QTermWidget *terminal = qobject_cast<QTermWidget *>(m_terminals->widget(index));
        resetTerminalTitleAction->setEnabled(m_terminalNames.contains(terminal));
        menu.addSeparator();
    }
    QAction *renameGroupAction = menu.addAction(QStringLiteral("Rename group"));
    QAction *closeGroupAction = menu.addAction(QStringLiteral("Close group"));

    QAction *selectedAction = menu.exec(m_terminals->tabBar()->mapToGlobal(position));
    if (selectedAction == newTerminalAction)
    {
        addTerminal();
    }
    else if (selectedAction == newGroupAction)
    {
        emit newGroupRequested();
    }
    else if (selectedAction == renameTerminalAction)
    {
        renameTerminal(index);
    }
    else if (selectedAction == resetTerminalTitleAction)
    {
        resetTerminalTitle(index);
    }
    else if (selectedAction == renameGroupAction)
    {
        emit renameGroupRequested();
    }
    else if (selectedAction == closeGroupAction)
    {
        emit closeGroupRequested();
    }
}

void TerminalGroup::updateTerminalTitle(QTermWidget *terminal)
{
    int index = m_terminals->indexOf(terminal);
    if (index < 0)
    {
        return;
    }

    QHash<QTermWidget *, QString>::const_iterator manualTitle = m_terminalNames.constFind(terminal);
    if (manualTitle != m_terminalNames.constEnd())
    {
        m_terminals->setTabText(index, manualTitle.value());
        return;
    }

    QString currentDirectory = directoryName(terminal->workingDirectory());
    QString context = terminal->isTitleChanged() ? terminal->title().trimmed() : processName(terminal->getForegroundProcessId());
    if (context.isEmpty())
    {
        context = processName(terminal->getShellPID());
    }

    QString title;
    if (!currentDirectory.isEmpty() && !context.isEmpty())
    {
        title = QStringLiteral("%1 : %2").arg(currentDirectory, context);
    }
    else
    {
        title = currentDirectory.isEmpty() ? context : currentDirectory;
    }

    if (!title.isEmpty())
    {
        m_terminals->setTabText(index, title);
    }
}
