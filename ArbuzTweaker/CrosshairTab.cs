namespace ArbuzTweaker;

public sealed class CrosshairTab : UserControl
{
    private readonly CrosshairPresetService _presetService = new(new ConfigService());
    private Button _toggleButton = null!;
    private NumericUpDown _sizeInput = null!;
    private NumericUpDown _gapInput = null!;
    private NumericUpDown _thicknessInput = null!;
    private NumericUpDown _opacityInput = null!;
    private TrackBar _sizeSlider = null!;
    private TrackBar _gapSlider = null!;
    private TrackBar _thicknessSlider = null!;
    private TrackBar _opacitySlider = null!;
    private ComboBox _shapeComboBox = null!;
    private ComboBox _presetComboBox = null!;
    private Button _savePresetButton = null!;
    private Button _addPresetButton = null!;
    private Button _renamePresetButton = null!;
    private Button _resetPresetButton = null!;
    private Button _deletePresetButton = null!;
    private ComboBox _colorComboBox = null!;
    private ComboBox _outlineColorComboBox = null!;
    private CheckBox _centerDotCheckBox = null!;
    private CheckBox _outlineCheckBox = null!;
    private Button _checkCenterButton = null!;
    private TableLayoutPanel _settingsLayout = null!;
    private Label _sizeLabel = null!;
    private Label _gapLabel = null!;
    private Label _thicknessLabel = null!;
    private Label _outlineColorLabel = null!;
    private Label _optionsLabel = null!;
    private Control _sizeSettingControl = null!;
    private Control _gapSettingControl = null!;
    private Control _thicknessSettingControl = null!;
    private Control _opacitySettingControl = null!;
    private Control _optionsSettingControl = null!;
    private Label _statusLabel = null!;
    private System.Windows.Forms.Timer _overlayKeepAliveTimer = null!;
    private CrosshairOverlayForm? _overlayForm;
    private Color _customColor = Color.White;
    private Color _customOutlineColor = Color.Black;
    private bool _isCrosshairEnabled;
    private bool _syncingNumericAndSlider;
    private bool _loadingPresetList;
    private bool _suppressCrosshairUpdate;
    private bool _suppressColorDialog;
    private bool _suppressOutlineColorDialog;

    public CrosshairTab()
    {
        InitializeComponent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _overlayKeepAliveTimer?.Stop();
            _overlayKeepAliveTimer?.Dispose();
            _overlayForm?.Dispose();
            _overlayForm = null;
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            AutoScroll = true,
            BackColor = UiTheme.Surface,
            Padding = new Padding(24, 22, 24, 22),
            ColumnCount = 1,
            RowCount = 7
        };
        root.SuspendLayout();
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Margin = new Padding(0, 0, 0, 10)
        };

        var usageLabel = new Label
        {
            Text = "Нажмите «Включить прицел». Если прицел уже включён, форма, размер и цвет меняются сразу. Если при входе в SCP:SL прицел пропадает, переключите игру в оконный режим или оконный режим без рамки. В полноэкранном режиме Windows-оверлеи могут скрываться игрой.",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = UiTheme.AccentGreen,
            MaximumSize = new Size(980, 0),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };

        var settingsPanel = UiTheme.CreateSectionPanel();
        settingsPanel.SuspendLayout();
        _settingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(0)
        };
        _settingsLayout.SuspendLayout();
        _settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        _settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var rowIndex = 0; rowIndex < _settingsLayout.RowCount; rowIndex++)
            _settingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _shapeComboBox = CreateShapeComboBox();
        _sizeInput = CreateNumericInput(4, 80, 14);
        _gapInput = CreateNumericInput(0, 40, 4);
        _thicknessInput = CreateNumericInput(1, 8, 2);
        _opacityInput = CreateNumericInput(0, 100, 100);
        _sizeSlider = CreateSlider(4, 80, 14);
        _gapSlider = CreateSlider(0, 40, 4);
        _thicknessSlider = CreateSlider(1, 8, 2);
        _opacitySlider = CreateSlider(0, 100, 100);
        _presetComboBox = CreateComboBox(220);
        _savePresetButton = CreateSmallButton("Сохранить");
        _addPresetButton = CreateSmallButton("Добавить");
        _renamePresetButton = CreateSmallButton("Переименовать", width: 160);
        _resetPresetButton = CreateSmallButton("По умолчанию", width: 150);
        _deletePresetButton = CreateSmallButton("Удалить", danger: true);
        _colorComboBox = CreateColorComboBox();
        _outlineColorComboBox = CreateColorComboBox(includeBlack: true);
        _centerDotCheckBox = CreateOptionCheckBox("Точка в центре", true);
        _outlineCheckBox = CreateOptionCheckBox("Обводка", false);

        AddSettingRow(_settingsLayout, 0, "Форма", _shapeComboBox);
        AddSettingRow(_settingsLayout, 1, "Шаблон", CreatePresetInput());
        _sizeSettingControl = CreateNumericSliderInput(_sizeInput, _sizeSlider);
        _gapSettingControl = CreateNumericSliderInput(_gapInput, _gapSlider);
        _thicknessSettingControl = CreateNumericSliderInput(_thicknessInput, _thicknessSlider);
        _opacitySettingControl = CreateNumericSliderInput(_opacityInput, _opacitySlider);
        _optionsSettingControl = CreateOptionsInput();
        _sizeLabel = AddSettingRow(_settingsLayout, 2, "Размер линий", _sizeSettingControl);
        _gapLabel = AddSettingRow(_settingsLayout, 3, "Отступ от центра", _gapSettingControl);
        _thicknessLabel = AddSettingRow(_settingsLayout, 4, "Толщина", _thicknessSettingControl);
        AddSettingRow(_settingsLayout, 5, "Прозрачность", _opacitySettingControl);
        AddSettingRow(_settingsLayout, 6, "Цвет", _colorComboBox);
        _optionsLabel = AddSettingRow(_settingsLayout, 7, "Опции", _optionsSettingControl);
        _outlineColorLabel = AddSettingRow(_settingsLayout, 8, "Цвет обводки", _outlineColorComboBox);
        SetSettingRowVisible(8, _outlineColorLabel, _outlineColorComboBox, false);
        settingsPanel.Controls.Add(_settingsLayout);

        _shapeComboBox.SelectedIndexChanged += ShapeComboBoxSelectedIndexChanged;
        _presetComboBox.SelectedIndexChanged += PresetComboBoxSelectedIndexChanged;
        _savePresetButton.Click += (s, e) => SaveCurrentPreset();
        _addPresetButton.Click += (s, e) => AddPreset();
        _renamePresetButton.Click += (s, e) => RenameSelectedPreset();
        _resetPresetButton.Click += (s, e) => ResetSelectedPresetToDefault();
        _deletePresetButton.Click += (s, e) => DeleteSelectedPreset();
        BindNumericAndSlider(_sizeInput, _sizeSlider);
        BindNumericAndSlider(_gapInput, _gapSlider);
        BindNumericAndSlider(_thicknessInput, _thicknessSlider);
        BindNumericAndSlider(_opacityInput, _opacitySlider);
        _colorComboBox.SelectedIndexChanged += ColorComboBoxSelectedIndexChanged;
        _outlineColorComboBox.SelectedIndexChanged += OutlineColorComboBoxSelectedIndexChanged;
        _centerDotCheckBox.CheckedChanged += CrosshairSettingChanged;
        _outlineCheckBox.CheckedChanged += OutlineCheckBoxCheckedChanged;

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

        _toggleButton = new Button
        {
            Text = "Включить прицел",
            Size = new Size(160, 35),
            Margin = new Padding(0)
        };
        UiTheme.StyleActionButton(_toggleButton, true);
        _toggleButton.Click += (s, e) => ToggleCrosshair();
        buttonsPanel.Controls.Add(_toggleButton);

        _checkCenterButton = new Button
        {
            Text = "Проверить центр",
            Size = new Size(160, 35),
            Margin = new Padding(10, 0, 0, 0)
        };
        UiTheme.StyleActionButton(_checkCenterButton);
        _checkCenterButton.Click += (s, e) => CheckCrosshairCenter();
        buttonsPanel.Controls.Add(_checkCenterButton);

        _statusLabel = new Label
        {
            Text = "Прицел выключен",
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            Margin = new Padding(0)
        };

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(warningLabel, 0, 1);
        root.Controls.Add(usageLabel, 0, 2);
        root.Controls.Add(settingsPanel, 0, 3);
        root.Controls.Add(buttonsPanel, 0, 4);
        root.Controls.Add(_statusLabel, 0, 5);

        RefreshPresetList();
        UpdateVisibleSettingsForShape();
        Controls.Add(root);

        _overlayKeepAliveTimer = new System.Windows.Forms.Timer { Interval = 700 };
        _overlayKeepAliveTimer.Tick += (s, e) => KeepCrosshairOverlayOnTop();

        _settingsLayout.ResumeLayout(false);
        settingsPanel.ResumeLayout(false);
        root.ResumeLayout(false);
        ResumeLayout(false);

        void ShowPreparedLayout()
        {
            if (IsDisposed || root.IsDisposed)
                return;

            root.Visible = true;
            _presetComboBox.Invalidate();
            _settingsLayout.Invalidate();
        }

        if (IsHandleCreated)
        {
            BeginInvoke((Action)ShowPreparedLayout);
        }
        else
        {
            EventHandler? handleCreated = null;
            handleCreated = (_, _) =>
            {
                HandleCreated -= handleCreated;
                BeginInvoke((Action)ShowPreparedLayout);
            };
            HandleCreated += handleCreated;
        }
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

    private static TrackBar CreateSlider(int min, int max, int value)
    {
        return new TrackBar
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickStyle = TickStyle.None,
            SmallChange = 1,
            LargeChange = Math.Max(1, (max - min) / 10),
            AutoSize = false,
            Height = 28,
            BackColor = UiTheme.SurfaceAlt,
            Margin = new Padding(0, 0, 0, 8),
            Dock = DockStyle.Fill
        };
    }

    private static Control CreateNumericSliderInput(NumericUpDown input, TrackBar slider)
    {
        input.Margin = new Padding(0, 0, 10, 8);

        var layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Height = 34,
            MinimumSize = new Size(360, 34),
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        layout.Controls.Add(input, 0, 0);
        layout.Controls.Add(slider, 1, 0);
        return layout;
    }

    private Control CreatePresetInput()
    {
        _presetComboBox.Margin = new Padding(0, 0, 0, 8);
        _presetComboBox.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = false,
            Size = new Size(600, 78),
            MinimumSize = new Size(600, 78),
            MaximumSize = new Size(int.MaxValue, 78),
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        buttonsPanel.Controls.Add(_savePresetButton);
        buttonsPanel.Controls.Add(_addPresetButton);
        buttonsPanel.Controls.Add(_renamePresetButton);
        buttonsPanel.Controls.Add(_resetPresetButton);
        buttonsPanel.Controls.Add(_deletePresetButton);

        layout.Controls.Add(_presetComboBox, 0, 0);
        layout.Controls.Add(buttonsPanel, 0, 1);
        return layout;
    }

    private Control CreateOptionsInput()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        panel.Controls.Add(_centerDotCheckBox);
        panel.Controls.Add(_outlineCheckBox);
        return panel;
    }

    private static CheckBox CreateOptionCheckBox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 22, 6)
        };
    }

    private static Button CreateSmallButton(string text, bool danger = false, int width = 116)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 32),
            Margin = new Padding(0, 0, 8, 6),
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };

        if (danger)
            StyleDangerButton(button);
        else
            UiTheme.StyleActionButton(button);

        return button;
    }

    private static void StyleDangerButton(Button button)
    {
        UiTheme.StyleActionButton(button);
        button.ForeColor = Color.FromArgb(255, 70, 70);
    }

    private void BindNumericAndSlider(NumericUpDown input, TrackBar slider)
    {
        input.ValueChanged += (s, e) =>
        {
            if (_syncingNumericAndSlider)
                return;

            _syncingNumericAndSlider = true;
            slider.Value = (int)input.Value;
            _syncingNumericAndSlider = false;
            CrosshairSettingChanged(s, e);
        };

        slider.ValueChanged += (s, e) =>
        {
            if (_syncingNumericAndSlider)
                return;

            _syncingNumericAndSlider = true;
            input.Value = slider.Value;
            _syncingNumericAndSlider = false;
            CrosshairSettingChanged(s, e);
        };
    }

    private static ComboBox CreateShapeComboBox()
    {
        var comboBox = CreateComboBox(190);
        comboBox.Items.AddRange(new object[]
        {
            "Классический",
            "Крест",
            "Точка",
            "Круг",
            "Круг с крестом",
            "Уголки",
            "T-образный"
        });
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static ComboBox CreateColorComboBox(bool includeBlack = false)
    {
        var comboBox = CreateComboBox(160);
        if (includeBlack)
            comboBox.Items.Add("Чёрный");

        comboBox.Items.AddRange(new object[]
        {
            "Белый",
            "Зелёный",
            "Красный",
            "Голубой",
            "Жёлтый",
            "Розовый",
            "Свой цвет"
        });
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static ComboBox CreateComboBox(int width)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = width,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private static Label AddSettingRow(TableLayoutPanel layout, int row, string labelText, Control input)
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
        return label;
    }

    private void UpdateVisibleSettingsForShape()
    {
        var shape = ResolveSelectedShape();
        var showGap = shape is CrosshairShape.Classic or CrosshairShape.CircleCross or CrosshairShape.Corners or CrosshairShape.TShape;
        var showThickness = shape != CrosshairShape.Dot;
        var showCenterDot = shape is CrosshairShape.Classic or CrosshairShape.Circle or CrosshairShape.CircleCross or CrosshairShape.Corners or CrosshairShape.TShape;

        _sizeLabel.Text = shape switch
        {
            CrosshairShape.Dot => "Размер точки",
            CrosshairShape.Circle => "Радиус круга",
            CrosshairShape.Corners => "Размер уголков",
            _ => "Размер линий"
        };

        SetSettingRowVisible(3, _gapLabel, _gapSettingControl, showGap);
        SetSettingRowVisible(4, _thicknessLabel, _thicknessSettingControl, showThickness);
        _centerDotCheckBox.Visible = showCenterDot;
        _outlineCheckBox.Visible = true;
        SetSettingRowVisible(7, _optionsLabel, _optionsSettingControl, showCenterDot || _outlineCheckBox.Visible);
        SetSettingRowVisible(8, _outlineColorLabel, _outlineColorComboBox, _outlineCheckBox.Checked);
    }

    private void SetSettingRowVisible(int row, Label label, Control input, bool visible)
    {
        label.Visible = visible;
        input.Visible = visible;
        _settingsLayout.RowStyles[row].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
        _settingsLayout.RowStyles[row].Height = visible ? 0F : 0F;
    }

    private void ShapeComboBoxSelectedIndexChanged(object? sender, EventArgs e)
    {
        RefreshPresetList();
        UpdateVisibleSettingsForShape();
    }

    private void PresetComboBoxSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingPresetList || _presetComboBox.SelectedItem is not CrosshairPresetListItem item)
            return;

        ApplyPreset(item.Preset);
    }

    private void CrosshairSettingChanged(object? sender, EventArgs e)
    {
        if (_suppressCrosshairUpdate)
            return;

        if (_isCrosshairEnabled)
            ShowOrUpdateCrosshair();
    }

    private void OutlineCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        UpdateVisibleSettingsForShape();
        CrosshairSettingChanged(sender, e);
    }

    private void ColorComboBoxSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressColorDialog)
        {
            CrosshairSettingChanged(sender, e);
            return;
        }

        if (_colorComboBox.SelectedItem?.ToString() != "Свой цвет")
        {
            CrosshairSettingChanged(sender, e);
            return;
        }

        var color = PromptForHexColor(this, _customColor);
        if (color == null)
        {
            SelectColor(ResolveSelectedColor().ToArgb());
            return;
        }

        _customColor = color.Value;
        CrosshairSettingChanged(sender, e);
        SetStatusMessage($"Выбран свой цвет #{_customColor.R:X2}{_customColor.G:X2}{_customColor.B:X2}.", UiTheme.AccentGreen);
    }

    private void OutlineColorComboBoxSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressOutlineColorDialog)
        {
            CrosshairSettingChanged(sender, e);
            return;
        }

        if (_outlineColorComboBox.SelectedItem?.ToString() != "Свой цвет")
        {
            CrosshairSettingChanged(sender, e);
            return;
        }

        var color = PromptForHexColor(this, _customOutlineColor);
        if (color == null)
        {
            SelectOutlineColor(ResolveSelectedOutlineColor().ToArgb());
            return;
        }

        _customOutlineColor = color.Value;
        CrosshairSettingChanged(sender, e);
        SetStatusMessage($"Выбран цвет обводки #{_customOutlineColor.R:X2}{_customOutlineColor.G:X2}{_customOutlineColor.B:X2}.", UiTheme.AccentGreen);
    }

    private void RefreshPresetList(string? selectName = null, bool applySelected = true, bool showAppliedStatus = false)
    {
        var preferredName = selectName ?? GetSelectedPresetItem()?.Preset.Name;
        _loadingPresetList = true;
        _presetComboBox.Items.Clear();

        var presets = _presetService.LoadPresets(ResolveSelectedShape());
        foreach (var preset in presets)
            _presetComboBox.Items.Add(new CrosshairPresetListItem(preset));

        _presetComboBox.Enabled = presets.Count > 0;
        _deletePresetButton.Enabled = true;

        if (presets.Count > 0)
        {
            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                for (var i = 0; i < _presetComboBox.Items.Count; i++)
                {
                    if (_presetComboBox.Items[i] is CrosshairPresetListItem item &&
                        string.Equals(item.Preset.Name, preferredName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            _presetComboBox.SelectedIndex = selectedIndex;
        }

        _loadingPresetList = false;

        if (applySelected && GetSelectedPresetItem() is { } selectedItem)
            ApplyPreset(selectedItem.Preset, showAppliedStatus);
    }

    private void SaveCurrentPreset()
    {
        if (GetSelectedPresetItem() is not { } item)
        {
            MessageBox.Show(this, "Выберите шаблон для сохранения.", "Шаблон прицела", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var settings = BuildSettings();
        _presetService.SavePreset(settings.Shape, CrosshairPresetData.FromSettings(item.Preset.Name, settings));
        RefreshPresetList(item.Preset.Name, applySelected: false);
        SetStatusMessage($"Шаблон «{item.Preset.Name}» сохранён.", UiTheme.AccentGreen);
    }

    private void AddPreset()
    {
        var name = PromptForPresetName(this, GetNextPresetName());
        if (name == null)
            return;

        name = name.Trim();
        if (!ValidateNewPresetName(name))
            return;

        var settings = BuildSettings();
        _presetService.SavePreset(settings.Shape, CrosshairPresetData.FromSettings(name, settings));
        RefreshPresetList(name, applySelected: false);
        SetStatusMessage($"Шаблон «{name}» добавлен.", UiTheme.AccentGreen);
    }

    private void RenameSelectedPreset()
    {
        if (GetSelectedPresetItem() is not { } item)
        {
            MessageBox.Show(this, "Выберите шаблон для переименования.", "Переименование шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var oldName = item.Preset.Name;
        var name = PromptForPresetName(this, oldName);
        if (name == null)
            return;

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Введите название шаблона.", "Переименование шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.Equals(oldName, name, StringComparison.CurrentCultureIgnoreCase))
            return;

        if (_presetService.PresetExists(ResolveSelectedShape(), name))
        {
            MessageBox.Show(this, "Шаблон с таким названием уже есть.", "Переименование шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _presetService.RenamePreset(ResolveSelectedShape(), oldName, name);
        RefreshPresetList(name, applySelected: false);
        SetStatusMessage($"Шаблон «{oldName}» переименован в «{name}».", UiTheme.AccentGreen);
    }

    private void ResetSelectedPresetToDefault()
    {
        if (GetSelectedPresetItem() is not { } item)
        {
            MessageBox.Show(this, "Выберите шаблон для сброса.", "Шаблон прицела", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var defaultPreset = CrosshairPresetService.CreateDefaultPreset();
        defaultPreset.Name = item.Preset.Name;

        ApplyPreset(defaultPreset, showStatus: false);
        _presetService.SavePreset(ResolveSelectedShape(), defaultPreset);
        RefreshPresetList(defaultPreset.Name, applySelected: false);
        CrosshairSettingChanged(this, EventArgs.Empty);
        SetStatusMessage($"Шаблон «{defaultPreset.Name}» сброшен к значениям по умолчанию.", UiTheme.AccentGreen);
    }

    private void DeleteSelectedPreset()
    {
        if (GetSelectedPresetItem() is not { } item)
        {
            MessageBox.Show(this, "Выберите шаблон для удаления.", "Удаление шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_presetComboBox.Items.Count <= 1)
        {
            MessageBox.Show(this, "Последний шаблон удалить нельзя. Можно сбросить его к значениям по умолчанию.", "Удаление шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Удалить шаблон «{item.Preset.Name}» для формы «{_shapeComboBox.Text}»?",
            "Удаление шаблона",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _presetService.DeletePreset(ResolveSelectedShape(), item.Preset.Name);
        RefreshPresetList(showAppliedStatus: false);
        SetStatusMessage($"Шаблон «{item.Preset.Name}» удалён.", UiTheme.TextDim);
    }

    private void ApplyPreset(CrosshairPresetData preset, bool showStatus = true)
    {
        _suppressCrosshairUpdate = true;
        SetNumericSliderValue(_sizeInput, _sizeSlider, preset.Size);
        SetNumericSliderValue(_gapInput, _gapSlider, preset.Gap);
        SetNumericSliderValue(_thicknessInput, _thicknessSlider, preset.Thickness);
        SetNumericSliderValue(_opacityInput, _opacitySlider, preset.OpacityPercent);
        SelectColor(preset.ColorArgb);
        SelectOutlineColor(preset.OutlineColorArgb);
        _centerDotCheckBox.Checked = preset.ShowCenterDot;
        _outlineCheckBox.Checked = preset.ShowOutline;
        _suppressCrosshairUpdate = false;
        UpdateVisibleSettingsForShape();

        CrosshairSettingChanged(this, EventArgs.Empty);
        if (showStatus)
            SetStatusMessage($"Применён шаблон «{preset.Name}».", UiTheme.AccentGreen);
    }

    private CrosshairPresetListItem? GetSelectedPresetItem()
    {
        return _presetComboBox.SelectedItem as CrosshairPresetListItem;
    }

    private bool ValidateNewPresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Введите название шаблона.", "Шаблон прицела", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (_presetService.PresetExists(ResolveSelectedShape(), name))
        {
            MessageBox.Show(this, "Шаблон с таким названием уже есть.", "Шаблон прицела", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        return true;
    }

    private string GetNextPresetName()
    {
        var usedNames = _presetComboBox.Items
            .OfType<CrosshairPresetListItem>()
            .Select(item => item.Preset.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        for (var index = 1; index < 1000; index++)
        {
            var name = $"Шаблон {index}";
            if (!usedNames.Contains(name))
                return name;
        }

        return "Новый шаблон";
    }

    private void SetNumericSliderValue(NumericUpDown input, TrackBar slider, int rawValue)
    {
        var value = Math.Clamp(rawValue, (int)input.Minimum, (int)input.Maximum);

        _syncingNumericAndSlider = true;
        input.Value = value;
        slider.Value = value;
        _syncingNumericAndSlider = false;
    }

    private void SelectColor(int colorArgb)
    {
        var colorName = ResolveColorName(colorArgb, includeBlack: false);
        _suppressColorDialog = true;
        if (colorName == null)
        {
            _customColor = Color.FromArgb(colorArgb);
            _colorComboBox.SelectedItem = "Свой цвет";
        }
        else
        {
            _colorComboBox.SelectedItem = colorName;
        }

        _suppressColorDialog = false;
    }

    private void SelectOutlineColor(int colorArgb)
    {
        var colorName = ResolveColorName(colorArgb, includeBlack: true);
        _suppressOutlineColorDialog = true;
        if (colorName == null)
        {
            _customOutlineColor = Color.FromArgb(colorArgb);
            _outlineColorComboBox.SelectedItem = "Свой цвет";
        }
        else
        {
            _outlineColorComboBox.SelectedItem = colorName;
        }

        _suppressOutlineColorDialog = false;
    }

    private void SetStatusMessage(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private void CheckCrosshairCenter()
    {
        if (!_isCrosshairEnabled)
            ShowOrUpdateCrosshair();

        var result = _overlayForm?.CheckCenter();
        if (result == null)
        {
            SetStatusMessage("Не удалось проверить центр прицела.", Color.FromArgb(255, 90, 90));
            return;
        }

        SetStatusMessage(result.Message, result.IsOk ? UiTheme.AccentGreen : Color.FromArgb(255, 90, 90));
    }

    private void ToggleCrosshair()
    {
        if (_isCrosshairEnabled)
            HideCrosshair();
        else
            ShowOrUpdateCrosshair();
    }

    private void ShowOrUpdateCrosshair()
    {
        var settings = BuildSettings();

        if (_overlayForm == null || _overlayForm.IsDisposed)
            _overlayForm = new CrosshairOverlayForm(settings);
        else
            _overlayForm.UpdateSettings(settings);

        if (!_overlayForm.Visible)
            _overlayForm.Show();

        _overlayForm.UpdateSettings(settings);
        _overlayForm.KeepOnTop();
        _overlayKeepAliveTimer.Start();
        _isCrosshairEnabled = true;
        UpdateCrosshairStateUi();
    }

    private void HideCrosshair()
    {
        _isCrosshairEnabled = false;
        _overlayKeepAliveTimer.Stop();
        if (_overlayForm != null && !_overlayForm.IsDisposed)
            _overlayForm.Hide();

        UpdateCrosshairStateUi();
    }

    private void KeepCrosshairOverlayOnTop()
    {
        if (!_isCrosshairEnabled || _overlayForm == null || _overlayForm.IsDisposed)
        {
            _overlayKeepAliveTimer.Stop();
            return;
        }

        _overlayForm.KeepOnTop();
    }

    private void UpdateCrosshairStateUi()
    {
        _toggleButton.Text = _isCrosshairEnabled ? "Скрыть прицел" : "Включить прицел";
        UiTheme.StyleActionButton(_toggleButton, !_isCrosshairEnabled);
        _statusLabel.Text = _isCrosshairEnabled
            ? "Прицел включён."
            : "Прицел выключен";
        _statusLabel.ForeColor = _isCrosshairEnabled ? UiTheme.AccentGreen : UiTheme.TextDim;
    }

    private CrosshairSettings BuildSettings()
    {
        return new CrosshairSettings(
            ResolveSelectedShape(),
            (int)_sizeInput.Value,
            (int)_gapInput.Value,
            (int)_thicknessInput.Value,
            (int)_opacityInput.Value,
            ResolveSelectedColor(),
            ResolveSelectedOutlineColor(),
            _centerDotCheckBox.Checked,
            _outlineCheckBox.Checked);
    }

    private CrosshairShape ResolveSelectedShape()
    {
        return _shapeComboBox.SelectedItem?.ToString() switch
        {
            "Крест" => CrosshairShape.Cross,
            "Точка" => CrosshairShape.Dot,
            "Круг" => CrosshairShape.Circle,
            "Круг с крестом" => CrosshairShape.CircleCross,
            "Уголки" => CrosshairShape.Corners,
            "T-образный" => CrosshairShape.TShape,
            _ => CrosshairShape.Classic
        };
    }

    private Color ResolveSelectedColor()
    {
        return _colorComboBox.SelectedItem?.ToString() switch
        {
            "Зелёный" => Color.Lime,
            "Красный" => Color.Red,
            "Голубой" => Color.DeepSkyBlue,
            "Жёлтый" => Color.Yellow,
            "Розовый" => Color.DeepPink,
            "Свой цвет" => _customColor,
            _ => Color.White
        };
    }

    private Color ResolveSelectedOutlineColor()
    {
        return _outlineColorComboBox.SelectedItem?.ToString() switch
        {
            "Чёрный" => Color.Black,
            "Зелёный" => Color.Lime,
            "Красный" => Color.Red,
            "Голубой" => Color.DeepSkyBlue,
            "Жёлтый" => Color.Yellow,
            "Розовый" => Color.DeepPink,
            "Свой цвет" => _customOutlineColor,
            _ => Color.White
        };
    }

    private static string? ResolveColorName(int colorArgb, bool includeBlack)
    {
        if (includeBlack && colorArgb == Color.Black.ToArgb())
            return "Чёрный";
        if (colorArgb == Color.Lime.ToArgb())
            return "Зелёный";
        if (colorArgb == Color.Red.ToArgb())
            return "Красный";
        if (colorArgb == Color.DeepSkyBlue.ToArgb())
            return "Голубой";
        if (colorArgb == Color.Yellow.ToArgb())
            return "Жёлтый";
        if (colorArgb == Color.DeepPink.ToArgb())
            return "Розовый";
        if (colorArgb == Color.White.ToArgb())
            return "Белый";

        return null;
    }

    private static string? PromptForPresetName(IWin32Window owner, string currentName)
    {
        using var form = new Form
        {
            Text = "Шаблон прицела",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 132),
            BackColor = UiTheme.Surface
        };

        var label = new Label
        {
            Text = "Название шаблона:",
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(14, 14)
        };

        var textBox = new TextBox
        {
            Text = currentName,
            Location = new Point(14, 40),
            Size = new Size(332, 27),
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var saveButton = new Button
        {
            Text = "Сохранить",
            DialogResult = DialogResult.OK,
            Location = new Point(146, 86),
            Size = new Size(96, 31)
        };
        UiTheme.StyleActionButton(saveButton, true);

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(250, 86),
            Size = new Size(96, 31)
        };
        UiTheme.StyleActionButton(cancelButton);

        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(saveButton);
        form.Controls.Add(cancelButton);
        form.AcceptButton = saveButton;
        form.CancelButton = cancelButton;

        textBox.SelectAll();
        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }

    private static Color? PromptForHexColor(IWin32Window owner, Color currentColor)
    {
        using var form = new Form
        {
            Text = "Свой цвет",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 144),
            BackColor = UiTheme.Surface
        };

        var label = new Label
        {
            Text = "HEX цвет (#RRGGBB):",
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(14, 14)
        };

        var textBox = new TextBox
        {
            Text = $"#{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}",
            Location = new Point(14, 42),
            Size = new Size(332, 27),
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var hintLabel = new Label
        {
            Text = "Пример: #FF00FF",
            ForeColor = UiTheme.TextDim,
            AutoSize = true,
            Location = new Point(14, 74)
        };

        var okButton = new Button
        {
            Text = "Применить",
            DialogResult = DialogResult.OK,
            Location = new Point(146, 100),
            Size = new Size(96, 31)
        };
        UiTheme.StyleActionButton(okButton, true);

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(250, 100),
            Size = new Size(96, 31)
        };
        UiTheme.StyleActionButton(cancelButton);

        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(hintLabel);
        form.Controls.Add(okButton);
        form.Controls.Add(cancelButton);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        while (form.ShowDialog(owner) == DialogResult.OK)
        {
            if (TryParseHexColor(textBox.Text, out var color))
                return color;

            MessageBox.Show(owner, "Введите цвет в формате #RRGGBB, например #FF00FF.", "Свой цвет", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return null;
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        color = Color.White;
        var hex = value.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return false;

        color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private sealed class CrosshairPresetListItem
    {
        public CrosshairPresetListItem(CrosshairPresetData preset)
        {
            Preset = preset;
        }

        public CrosshairPresetData Preset { get; }

        public override string ToString()
        {
            return Preset.Name;
        }
    }
}
