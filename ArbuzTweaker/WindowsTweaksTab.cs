using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ArbuzTweaker;

public partial class WindowsTweaksTab : UserControl
{
    private const string NduRegistryPath = @"SYSTEM\CurrentControlSet\Services\Ndu";
    private CheckBox _nduCheckBox = null!;
    private Label _stateLabel = null!;
    private Label _statusLabel = null!;

    public WindowsTweaksTab()
    {
        InitializeComponent();
        LoadState();
    }

    private void InitializeComponent()
    {
        var titleLabel = new Label
        {
            Text = "Твики Windows",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };

        var infoLabel = new Label
        {
            Text = "Здесь находятся системные твики Windows. Некоторые из них требуют запуск твикера от имени администратора.",
            Location = new Point(20, 55),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var tweakLabel = new Label
        {
            Text = "Сеть",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 95),
            AutoSize = true
        };

        _nduCheckBox = new CheckBox
        {
            Text = "Устранение сетевой утечки",
            Location = new Point(20, 125),
            AutoSize = true,
            ForeColor = Color.White
        };

        var descriptionLabel = new Label
        {
            Text = "Меняет значение Start у службы Ndu. При включении ставит 4, что может снизить рост потребления памяти из-за Ndu.",
            Location = new Point(20, 152),
            MaximumSize = new Size(820, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _stateLabel = new Label
        {
            Text = "Текущее состояние: неизвестно",
            Location = new Point(20, 198),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 230),
            Size = new Size(120, 35)
        };
        applyButton.Click += async (s, e) => await ApplyNduSettingAsync();

        _statusLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(20, 280),
            AutoSize = true,
            ForeColor = Color.Green
        };

        Controls.Add(titleLabel);
        Controls.Add(infoLabel);
        Controls.Add(tweakLabel);
        Controls.Add(_nduCheckBox);
        Controls.Add(descriptionLabel);
        Controls.Add(_stateLabel);
        Controls.Add(applyButton);
        Controls.Add(_statusLabel);
    }

    private void LoadState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(NduRegistryPath, false);
            var currentValue = key?.GetValue("Start");
            if (currentValue is int startValue)
            {
                _nduCheckBox.Checked = startValue == 4;
                _stateLabel.Text = $"Текущее значение Start: {startValue}";
                _stateLabel.ForeColor = Color.Gainsboro;
                return;
            }

            _stateLabel.Text = "Не удалось прочитать значение Start";
            _stateLabel.ForeColor = Color.Orange;
        }
        catch
        {
            _stateLabel.Text = "Нет доступа к чтению реестра";
            _stateLabel.ForeColor = Color.Orange;
        }
    }

    private async Task ApplyNduSettingAsync()
    {
        var targetValue = _nduCheckBox.Checked ? 4 : 2;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(NduRegistryPath, true);
            if (key == null)
            {
                ShowStatus("Не удалось открыть раздел Ndu", Color.Orange);
                return;
            }

            key.SetValue("Start", targetValue, RegistryValueKind.DWord);
            _stateLabel.Text = $"Текущее значение Start: {targetValue}";
            _stateLabel.ForeColor = Color.Gainsboro;
            ShowStatus("Твик применён", Color.Green);
        }
        catch (UnauthorizedAccessException)
        {
            ShowStatus("Нужен запуск от имени администратора", Color.Orange);
        }
        catch
        {
            ShowStatus("Не удалось изменить параметр Ndu", Color.Orange);
        }

        await Task.CompletedTask;
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2000);
        _statusLabel.Text = string.Empty;
    }
}
