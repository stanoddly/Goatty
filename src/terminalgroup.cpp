#include "terminalgroup.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QInputDialog>
#include <QLineEdit>
#include <QMenu>
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

    connect(terminal, SIGNAL(finished()), this, SLOT(terminalFinished()));
    connect(terminal, SIGNAL(titleChanged()), this, SLOT(terminalStateChanged()));
    connect(terminal, SIGNAL(currentDirectoryChanged(QString)), this, SLOT(terminalStateChanged()));

    int index = m_terminals->addTab(terminal, QStringLiteral("Shell %1").arg(m_nextTerminalNumber++));
    m_terminals->setCurrentIndex(index);
    QTimer *titleTimer = new QTimer(terminal);
    titleTimer->setInterval(1000);
    connect(titleTimer, &QTimer::timeout, this, [this, terminal]() {
        updateTerminalTitle(terminal);
    });
    titleTimer->start();

    QTimer::singleShot(0, terminal, [this, terminal]() {
        terminal->startShellProgram();
        terminal->setFocus();
        updateTerminalTitle(terminal);
    });
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

void TerminalGroup::closeTerminal(int index)
{
    QWidget *terminal = m_terminals->widget(index);
    if (terminal == nullptr)
    {
        return;
    }

    m_terminals->removeTab(index);
    m_terminalNames.remove(qobject_cast<QTermWidget *>(terminal));
    terminal->deleteLater();

    if (m_terminals->count() == 0)
    {
        emit emptied();
    }
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
