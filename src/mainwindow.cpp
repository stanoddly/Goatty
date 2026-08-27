#include "mainwindow.h"

#include "terminalgroup.h"

#include <QApplication>
#include <QDir>
#include <QFileInfo>
#include <QInputDialog>
#include <QKeySequence>
#include <QLineEdit>
#include <QMenu>
#include <QShortcut>
#include <QString>
#include <QTabBar>
#include <QTabWidget>
#include <QWidget>

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
    , m_groups(new QTabWidget(this))
{
    setWindowTitle(QApplication::applicationDisplayName());

    m_groups->setDocumentMode(true);
    m_groups->setMovable(true);
    m_groups->setTabsClosable(true);
    m_groups->setTabBarAutoHide(true);
    m_groups->tabBar()->setExpanding(false);
    m_groups->tabBar()->setContextMenuPolicy(Qt::CustomContextMenu);
    setCentralWidget(m_groups);

    connect(m_groups, &QTabWidget::tabCloseRequested, this, &MainWindow::closeGroup);
    connect(m_groups, &QTabWidget::tabBarDoubleClicked, this, &MainWindow::handleGroupDoubleClick);
    connect(m_groups->tabBar(), &QTabBar::customContextMenuRequested, this, &MainWindow::showGroupContextMenu);
    connect(m_groups, &QTabWidget::currentChanged, this, [this](int index) {
        TerminalGroup *group = qobject_cast<TerminalGroup *>(m_groups->widget(index));
        if (group != nullptr)
        {
            group->focusCurrentTerminal();
        }
    });

    QShortcut *newTabShortcut = new QShortcut(QKeySequence(Qt::CTRL | Qt::Key_T), this);
    connect(newTabShortcut, &QShortcut::activated, this, [this]() {
        TerminalGroup *group = qobject_cast<TerminalGroup *>(m_groups->currentWidget());
        if (group != nullptr)
        {
            group->addTerminal();
        }
    });

    QShortcut *newGroupShortcut = new QShortcut(QKeySequence(Qt::CTRL | Qt::SHIFT | Qt::Key_T), this);
    connect(newGroupShortcut, &QShortcut::activated, this, &MainWindow::addGroup);

    QShortcut *closeTabShortcut = new QShortcut(QKeySequence(Qt::CTRL | Qt::Key_W), this);
    connect(closeTabShortcut, &QShortcut::activated, this, [this]() {
        TerminalGroup *group = qobject_cast<TerminalGroup *>(m_groups->currentWidget());
        if (group != nullptr)
        {
            group->closeCurrentTerminal();
        }
    });

    QShortcut *nextTabShortcut = new QShortcut(QKeySequence::NextChild, this);
    connect(nextTabShortcut, &QShortcut::activated, this, [this]() {
        TerminalGroup *group = qobject_cast<TerminalGroup *>(m_groups->currentWidget());
        if (group != nullptr)
        {
            group->selectNextTerminal();
        }
    });

    QShortcut *previousTabShortcut = new QShortcut(QKeySequence::PreviousChild, this);
    connect(previousTabShortcut, &QShortcut::activated, this, [this]() {
        TerminalGroup *group = qobject_cast<TerminalGroup *>(m_groups->currentWidget());
        if (group != nullptr)
        {
            group->selectPreviousTerminal();
        }
    });

    addGroup();
}

void MainWindow::addGroup()
{
    TerminalGroup *group = new TerminalGroup;
    connect(group, &TerminalGroup::emptied, this, [this, group]() {
        closeGroup(m_groups->indexOf(group));
    });
    connect(group, &TerminalGroup::newGroupRequested, this, &MainWindow::addGroup);
    connect(group, &TerminalGroup::renameGroupRequested, this, [this, group]() {
        renameGroup(m_groups->indexOf(group));
    });
    connect(group, &TerminalGroup::closeGroupRequested, this, [this, group]() {
        closeGroup(m_groups->indexOf(group));
    });

    QString groupName = QFileInfo(QDir::currentPath()).fileName();
    if (groupName.isEmpty())
    {
        groupName = QDir::currentPath() == QStringLiteral("/") ? QStringLiteral("/") : QStringLiteral("Group %1").arg(m_nextGroupNumber);
    }

    ++m_nextGroupNumber;
    int index = m_groups->addTab(group, groupName);
    m_groups->setCurrentIndex(index);
    group->focusCurrentTerminal();
}

void MainWindow::closeGroup(int index)
{
    QWidget *group = m_groups->widget(index);
    if (group == nullptr)
    {
        return;
    }

    m_groups->removeTab(index);
    group->deleteLater();

    if (m_groups->count() == 0)
    {
        close();
    }
}

void MainWindow::handleGroupDoubleClick(int index)
{
    if (index < 0)
    {
        addGroup();
        return;
    }

    renameGroup(index);
}

void MainWindow::renameGroup(int index)
{
    if (index < 0 || index >= m_groups->count())
    {
        return;
    }

    bool accepted = false;
    QString title = QInputDialog::getText(this, QStringLiteral("Rename group"), QStringLiteral("Group name:"), QLineEdit::Normal, m_groups->tabText(index), &accepted).trimmed();
    if (accepted && !title.isEmpty())
    {
        m_groups->setTabText(index, title);
    }
}

void MainWindow::showGroupContextMenu(const QPoint &position)
{
    int index = m_groups->tabBar()->tabAt(position);
    if (index >= 0)
    {
        m_groups->setCurrentIndex(index);
    }

    QMenu menu(this);
    QAction *newGroupAction = menu.addAction(QStringLiteral("New group"));
    QAction *renameGroupAction = nullptr;
    QAction *closeGroupAction = nullptr;
    if (index >= 0)
    {
        menu.addSeparator();
        renameGroupAction = menu.addAction(QStringLiteral("Rename group"));
        closeGroupAction = menu.addAction(QStringLiteral("Close group"));
    }

    QAction *selectedAction = menu.exec(m_groups->tabBar()->mapToGlobal(position));
    if (selectedAction == newGroupAction)
    {
        addGroup();
    }
    else if (selectedAction == renameGroupAction)
    {
        renameGroup(index);
    }
    else if (selectedAction == closeGroupAction)
    {
        closeGroup(index);
    }
}
