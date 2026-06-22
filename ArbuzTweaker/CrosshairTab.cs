namespace ArbuzTweaker;

public sealed class CrosshairTab : UserControl
{
    private CheckBox _enabledCheckBox = null!;
    private NumericUpDown _sizeInput = null!;
    private NumericUpDown _gapInput = null!;
    private NumericUpDown _thicknessInput = null!;
    private ComboBox _colorComboBox = null!;
    private Label _statusLabel = null!;
    private CrosshairOverlayForm? _overlayForm;

    public CrosshairTab()
    {
        InitializeComponent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _overlayForm?.Dispose();
            _overlayForm = null;
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.Surface,
            Padding = new Padding(24, 22, 24, 22),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Прицел",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var warningLabel = new Label
        {
            Text = "Экранный оверлей поверх игр и приложений. Он не лезет в процесс игры, не делает инжект и не перехватывает ввод, но перед использованием в онлайн-играх стоит проверить правила конкретной игры.",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = UiTheme.TextMuted,
            MaximumSize = new Size(760, 0),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };

        var settingsPanel = UiTheme.CreateSectionPanel();
        var settingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(0)
        };
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _enabledCheckBox = new CheckBox
        {
            Text = "Показывать прицел",
            Checked = false,
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 10)
        };

        _sizeInput = CreateNumericInput(4, 80, 14);
        _gapInput = CreateNumericInput(0, 40, 4);
        _thicknessInput = CreateNumericInput(1, 8, 2);
        _colorComboBox = CreateColorComboBox();

        settingsLayout.Controls.Add(_enabledCheckBox, 0, 0);
        settingsLayout.SetColumnSpan(_enabledCheckBox, 2);
        AddSettingRow(settingsLayout, 1, "Размер линий", _sizeInput);
        AddSettingRow(settingsLayout, 2, "Отступ от центра", _gapInput);
        AddSettingRow(settingsLayout, 3, "Толщина", _thicknessInput);
        AddSettingRow(settingsLayout, 4, "Цвет", _colorComboBox);
        settingsPanel.Controls.Add(settingsLayout);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };

        var applyButton = new Button
        {
            Text = "Применить",
            Size = new Size(130, 35),
            Margin = new Padding(0, 0, 10, 0)
        };
        UiTheme.StyleActionButton(applyButton, true);
        applyButton.Click += (s, e) => ApplyCrosshair();

        var hideButton = new Button
        {
            Text = "Скрыть",
            Size = new Size(110, 35),
            Margin = new Padding(0)
        };
        UiTheme.StyleActionButton(hideButton);
        hideButton.Click += (s, e) => HideCrosshair();

        buttonsPanel.Controls.Add(applyButton);
        buttonsPanel.Controls.Add(hideButton);

        _statusLabel = new Label
        {
            Text = "Прицел выключен",
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            Margin = new Padding(0)
        };

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(warningLabel, 0, 1);
        root.Controls.Add(settingsPanel, 0, 2);
        root.Controls.Add(buttonsPanel, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);
        Controls.Add(root);
    }

    private static NumericUpDown CreateNumericInput(int min, int max, int value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 90,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private static ComboBox CreateColorComboBox()
    {
        var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 0)
        };

        comboBox.Items.AddRange(new object[]
        {
            "Белый",
            "Зелёный",
            "Красный",
            "Голубой",
            "Жёлтый"
        });
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static void AddSettingRow(TableLayoutPanel layout, int row, string labelText, Control input)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 4, 14, 8)
        };

        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private void ApplyCrosshair()
    {
        if (!_enabledCheckBox.Checked)
        {
            HideCrosshair();
            return;
        }

        var settings = new CrosshairSettings(
            (int)_sizeInput.Value,
            (int)_gapInput.Value,
            (int)_thicknessInput.Value,
            ResolveSelectedColor());

        if (_overlayForm == null || _overlayForm.IsDisposed)
            _overlayForm = new CrosshairOverlayForm(settings);
        else
            _overlayForm.UpdateSettings(settings);

        if (!_overlayForm.Visible)
        {
            var owner = FindForm();
            if (owner != null)
                _overlayForm.Show(owner);
            else
                _overlayForm.Show();
        }

        _overlayForm.UpdateSettings(settings);
        _overlayForm.BringToFront();
        _statusLabel.Text = "Прицел включён";
        _statusLabel.ForeColor = UiTheme.AccentGreen;
    }

    private void HideCrosshair()
    {
        _enabledCheckBox.Checked = false;
        if (_overlayForm != null && !_overlayForm.IsDisposed)
            _overlayForm.Hide();

        _statusLabel.Text = "Прицел выключен";
        _statusLabel.ForeColor = UiTheme.TextDim;
    }

    private Color ResolveSelectedColor()
    {
        return _colorComboBox.SelectedItem?.ToString() switch
        {
            "Зелёный" => Color.Lime,
            "Красный" => Color.Red,
            "Голубой" => Color.DeepSkyBlue,
            "Жёлтый" => Color.Yellow,
            _ => Color.White
        };
    }
}
