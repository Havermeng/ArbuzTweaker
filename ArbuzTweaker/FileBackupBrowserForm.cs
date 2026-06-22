namespace ArbuzTweaker;

public sealed class FileBackupBrowserForm : Form
{
    private readonly FileBackupService _fileBackupService;
    private readonly string? _categoryFilter;
    private readonly ListView _backupListView;
    private readonly Label _statusLabel;

    public FileBackupBrowserForm(FileBackupService fileBackupService, string? categoryFilter = null)
    {
        _fileBackupService = fileBackupService;
        _categoryFilter = categoryFilter;

        Text = string.IsNullOrWhiteSpace(_categoryFilter)
            ? "Файловые бэкапы"
            : $"Файловые бэкапы: {_categoryFilter}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 620);
        MinimumSize = new Size(760, 460);
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 10);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle());
        root.RowStyles.Add(new RowStyle());

        _backupListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        _backupListView.Columns.Add("Дата", 150);
        _backupListView.Columns.Add("Категория", 170);
        _backupListView.Columns.Add("Исходный файл", 360);
        _backupListView.Columns.Add("Бэкап", 260);
        _backupListView.DoubleClick += (s, e) => RestoreSelectedBackup();

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 10, 0, 8)
        };

        var restoreButton = new Button { Text = "Восстановить", Size = new Size(140, 35), Margin = new Padding(0, 0, 10, 0) };
        restoreButton.Click += (s, e) => RestoreSelectedBackup();

        var refreshButton = new Button { Text = "Обновить", Size = new Size(120, 35), Margin = new Padding(0, 0, 10, 0) };
        refreshButton.Click += (s, e) => LoadBackups();

        var openFolderButton = new Button { Text = "Открыть папку", Size = new Size(140, 35), Margin = new Padding(0, 0, 10, 0) };
        openFolderButton.Click += (s, e) => _fileBackupService.OpenBackupFolder();

        var closeButton = new Button { Text = "Закрыть", Size = new Size(110, 35), Margin = new Padding(0) };
        closeButton.Click += (s, e) => Close();

        UiTheme.StyleActionButton(restoreButton, true);
        UiTheme.StyleActionButton(refreshButton);
        UiTheme.StyleActionButton(openFolderButton);
        UiTheme.StyleActionButton(closeButton);

        buttonsPanel.Controls.Add(restoreButton);
        buttonsPanel.Controls.Add(refreshButton);
        buttonsPanel.Controls.Add(openFolderButton);
        buttonsPanel.Controls.Add(closeButton);

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = string.Empty,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0)
        };

        root.Controls.Add(_backupListView, 0, 0);
        root.Controls.Add(buttonsPanel, 0, 1);
        root.Controls.Add(_statusLabel, 0, 2);

        Controls.Add(root);
        LoadBackups();
    }

    private void LoadBackups()
    {
        _backupListView.BeginUpdate();
        _backupListView.Items.Clear();

        foreach (var entry in _fileBackupService.ListBackups().Where(IsVisibleBackup))
        {
            var item = new ListViewItem(entry.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"))
            {
                Tag = entry
            };
            item.SubItems.Add(entry.Category);
            item.SubItems.Add(entry.OriginalPath);
            item.SubItems.Add(entry.BackupPath);
            _backupListView.Items.Add(item);
        }

        _backupListView.EndUpdate();
        _statusLabel.Text = _backupListView.Items.Count == 0
            ? "Новых бэкапов с манифестом пока нет."
            : $"Бэкапов в списке: {_backupListView.Items.Count}";
    }

    private bool IsVisibleBackup(FileBackupEntry entry)
    {
        return string.IsNullOrWhiteSpace(_categoryFilter)
            || entry.Category.Contains(_categoryFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void RestoreSelectedBackup()
    {
        if (_backupListView.SelectedItems.Count == 0 ||
            _backupListView.SelectedItems[0].Tag is not FileBackupEntry entry)
        {
            _statusLabel.Text = "Выбери бэкап для восстановления.";
            return;
        }

        var result = MessageBox.Show(
            $"Восстановить файл из бэкапа?\n\nИсходный путь:\n{entry.OriginalPath}\n\nБэкап:\n{entry.BackupPath}",
            "Подтверждение восстановления",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        if (_fileBackupService.RestoreBackup(entry))
        {
            _statusLabel.Text = "Файл восстановлен. Текущая версия перед заменой тоже сохранена в бэкап.";
            LoadBackups();
            return;
        }

        _statusLabel.Text = "Не удалось восстановить файл.";
    }
}
