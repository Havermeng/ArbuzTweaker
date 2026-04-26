using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ArbuzTweaker;

public partial class WindowsTweaksTab : UserControl
{
    private const string NduRegistryPath = @"SYSTEM\CurrentControlSet\Services\Ndu";
    private const string TcpipParametersRegistryPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    private const string EdgePolicyRegistryPath = @"SOFTWARE\Policies\Microsoft\Edge";

    private CheckBox _nduCheckBox = null!;
    private CheckBox _dhcpMediaSenseCheckBox = null!;
    private CheckBox _googleDnsCheckBox = null!;
    private CheckBox _disableIpv6CheckBox = null!;
    private CheckBox _edgeStartupBoostCheckBox = null!;
    private Panel _gameTweaksPanel = null!;
    private Label _nduStateLabel = null!;
    private Label _dhcpMediaSenseStateLabel = null!;
    private Label _googleDnsStateLabel = null!;
    private Label _ipv6StateLabel = null!;
    private Label _edgeStateLabel = null!;
    private Label _statusLabel = null!;
    private readonly Dictionary<RegistryGameTweak, CheckBox> _gameTweakCheckBoxes = new();

    private static readonly RegistryGameTweak[] GameTweaks =
    {
        new(
            "GPU Priority Scheduling (NVIDIA)",
            "Увеличивает GPU Priority в Multimedia SystemProfile. Включение ставит 8, выключение возвращает 2.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "GPU Priority", 8, 2) }),
        new(
            "Автоматический игровой режим Windows",
            "Включает Game Mode через параметры GameBar: AllowAutoGameMode и AutoGameModeEnabled.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, 0),
                RegistryGameValue.CurrentUser(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, 0)
            }),
        new(
            "Аппаратное планирование GPU (HAGS)",
            "Включает HwSchMode=2 в GraphicsDrivers. Для полного эффекта может потребоваться перезагрузка.",
            true,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GraphicsDrivers", "HwSchMode", 2, 1)
            }),
        new(
            "Оптимизация полноэкранных окон",
            "Включает параметры GameConfigStore для режима Fullscreen Optimizations.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"System\GameConfigStore", "GameDVR_FSEBehavior", 2, 0),
                RegistryGameValue.CurrentUser(@"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1, 0),
                RegistryGameValue.CurrentUser(@"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1, 0)
            }),
        new(
            "Режим Не беспокоить",
            "Отключает всплывающие уведомления и toast-уведомления на время работы системы.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_TOASTS_ENABLED", 0, 1),
                RegistryGameValue.CurrentUser(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0, 1),
                RegistryGameValue.LocalMachine(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0, 1)
            }),
        new(
            "Режим низкой задержки DWM",
            "Ставит OverlayTestMode=5 и UseOLEDTaskMode=1 в DWM.",
            true,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5, 0),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "UseOLEDTaskMode", 1, 0)
            }),
        new(
            "Выгрузка неиспользуемых DLL",
            "Добавляет AlwaysUnloadDll для Explorer в HKCU и HKLM. При выключении параметр удаляется.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "AlwaysUnloadDll", 1, null),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "AlwaysUnloadDll", 1, null)
            }),
        new(
            "Отключение Game DVR",
            "Отключает встроенную запись игрового процесса Windows Game DVR.",
            true,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\PolicyManager\default\ApplicationManagement\AllowGameDVR", "Value", 0, 1),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AllowGameDVR", 0, 1)
            }),
        new(
            "Отключение V-Sync в DirectX",
            "Ставит DisableVSync=1 в Direct3D Global.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"Software\Microsoft\Direct3D\Global", "DisableVSync", 1, 0) }),
        new(
            "Отключение аппаратного наложения DWM",
            "Ставит ForceDisableOverlay=1. Может помочь старым играм, но на некоторых системах лучше оставить выключенным.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "ForceDisableOverlay", 1, 0) }),
        new(
            "Отключение приоритета фоновых задач",
            "Ставит SystemResponsiveness=0, чтобы Multimedia SystemProfile отдавал больше ресурсов активным задачам.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, 20) }),
        new(
            "Отключение Power Throttling",
            "Ставит PowerThrottlingOff=1, отключая ограничение мощности фоновых процессов.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, 0) }),
        new(
            "Отключение энергосбережения USB",
            "Ставит DisableSelectiveSuspend=1 для USB, чтобы уменьшить задержки устройств ввода.",
            false,
            new[] { RegistryGameValue.LocalMachine(@"System\CurrentControlSet\Services\USB", "DisableSelectiveSuspend", 1, 0) }),
        new(
            "Увеличение приоритета игр",
            "Ставит NetworkThrottlingIndex=ffffffff и Priority=6 для Tasks\\Games.",
            false,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xffffffff), 10),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6, 2)
            }),
        new(
            "Ускорение обработки мыши",
            "Отключает EnhancedPointerPrecision в разделе класса устройств мыши.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"System\CurrentControlSet\Control\Class\{4D36E96F-E325-11CE-BFC1-08002BE10318}", "EnhancedPointerPrecision", 0, 1) }),
        new(
            "Ускорение работы видеокарты DWM",
            "Ставит EnableHWAcceleration=1 в DWM.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "EnableHWAcceleration", 1, 0) })
    };

    public WindowsTweaksTab()
    {
        InitializeComponent();
        LoadState();
    }

    private void InitializeComponent()
    {
        AutoScroll = true;
        BackColor = UiTheme.Surface;

        var titleLabel = new Label
        {
            Text = "Твики Windows",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary
        };

        var infoLabel = new Label
        {
            Text = "Здесь находятся системные твики Windows. Некоторые из них требуют запуск твикера от имени администратора. Раздел ещё находится в разработке, новые твики будут добавляться со временем.",
            Location = new Point(20, 55),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted
        };

        var memoryLabel = new Label
        {
            Text = "Память и фоновые процессы",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 105),
            AutoSize = true
        };

        _nduCheckBox = new CheckBox
        {
            Text = "Устранение сетевой утечки (Ndu)",
            Location = new Point(20, 135),
            AutoSize = true,
            ForeColor = Color.White
        };

        var nduDescriptionLabel = new Label
        {
            Text = "Меняет значение Start у службы Ndu. При включении ставит 4, что может снизить рост потребления памяти из-за Ndu.",
            Location = new Point(20, 162),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _nduStateLabel = new Label
        {
            Text = "Текущее значение Ndu Start: неизвестно",
            Location = new Point(20, 208),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyNduButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 238),
            Size = new Size(120, 35)
        };
        applyNduButton.Click += async (s, e) => await ApplyNduSettingAsync();

        var edgeLabel = new Label
        {
            Text = "Microsoft Edge",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 300),
            AutoSize = true
        };

        _edgeStartupBoostCheckBox = new CheckBox
        {
            Text = "Отключить Edge Startup Boost",
            Location = new Point(20, 330),
            AutoSize = true,
            ForeColor = Color.White
        };

        var edgeDescriptionLabel = new Label
        {
            Text = "Создаёт или меняет параметр StartupBoostEnabled в политике Edge. При включении ставит 0, чтобы Edge не подгружался в фоне заранее.",
            Location = new Point(20, 357),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _edgeStateLabel = new Label
        {
            Text = "Текущее значение StartupBoostEnabled: неизвестно",
            Location = new Point(20, 403),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyEdgeButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 433),
            Size = new Size(120, 35)
        };
        applyEdgeButton.Click += async (s, e) => await ApplyEdgeStartupBoostAsync();

        var repairNetworkLabel = new Label
        {
            Text = "Быстрое восстановление сети",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 500),
            AutoSize = true
        };

        var repairNetworkDescriptionLabel = new Label
        {
            Text = "Запускает стандартные команды Windows для восстановления сетевого стека: сброс Winsock, TCP/IP, DNS-кэша, release/renew IP и сброс WinHTTP proxy. Это безопасный базовый набор, если интернет периодически пропадает.",
            Location = new Point(20, 530),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var repairNetworkButton = new Button
        {
            Text = "Исправить проблемы сети",
            Location = new Point(20, 592),
            Size = new Size(190, 35)
        };
        repairNetworkButton.Click += async (s, e) => await RepairNetworkAsync();

        var restartAdaptersLabel = new Label
        {
            Text = "Перезапуск сетевого адаптера",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 665),
            AutoSize = true
        };

        var restartAdaptersDescriptionLabel = new Label
        {
            Text = "Отключает и заново включает все активные физические сетевые адаптеры. Полезно как быстрый ручной аналог переподключения кабеля или адаптера.",
            Location = new Point(20, 695),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var restartAdaptersButton = new Button
        {
            Text = "Перезапустить адаптер",
            Location = new Point(20, 745),
            Size = new Size(180, 35)
        };
        restartAdaptersButton.Click += async (s, e) => await RestartNetworkAdaptersAsync();

        var stabilityLabel = new Label
        {
            Text = "Стабильность подключения",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 820),
            AutoSize = true
        };

        _dhcpMediaSenseCheckBox = new CheckBox
        {
            Text = "DisableDHCPMediaSense",
            Location = new Point(20, 850),
            AutoSize = true,
            ForeColor = Color.White
        };

        var dhcpMediaSenseDescriptionLabel = new Label
        {
            Text = "Создаёт или меняет параметр DisableDHCPMediaSense в TCP/IP. Иногда помогает при обрывах проводного подключения, когда Windows слишком агрессивно реагирует на краткие потери линка.",
            Location = new Point(20, 877),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _dhcpMediaSenseStateLabel = new Label
        {
            Text = "Текущее значение DisableDHCPMediaSense: неизвестно",
            Location = new Point(20, 940),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyDhcpMediaSenseButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 970),
            Size = new Size(120, 35)
        };
        applyDhcpMediaSenseButton.Click += async (s, e) => await ApplyDhcpMediaSenseAsync();

        _googleDnsCheckBox = new CheckBox
        {
            Text = "Использовать Google DNS (8.8.8.8 / 8.8.4.4)",
            Location = new Point(20, 1040),
            AutoSize = true,
            ForeColor = Color.White
        };

        var googleDnsDescriptionLabel = new Label
        {
            Text = "Назначает активным физическим адаптерам публичные DNS Google. Это может помочь, если проблема связана именно с резолвингом DNS, а не с самим подключением.",
            Location = new Point(20, 1067),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _googleDnsStateLabel = new Label
        {
            Text = "Состояние Google DNS: неизвестно",
            Location = new Point(20, 1130),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyGoogleDnsButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1160),
            Size = new Size(120, 35)
        };
        applyGoogleDnsButton.Click += async (s, e) => await ApplyGoogleDnsAsync();

        _disableIpv6CheckBox = new CheckBox
        {
            Text = "Отключить IPv6 на активных физических адаптерах",
            Location = new Point(20, 1230),
            AutoSize = true,
            ForeColor = Color.White
        };

        var ipv6DescriptionLabel = new Label
        {
            Text = "Отключает IPv6 через привязку сетевого адаптера. Иногда помогает, если проблема вызвана конфликтами IPv4/IPv6 или странным поведением провайдера/роутера.",
            Location = new Point(20, 1257),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _ipv6StateLabel = new Label
        {
            Text = "Состояние IPv6: неизвестно",
            Location = new Point(20, 1320),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyIpv6Button = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1350),
            Size = new Size(120, 35)
        };
        applyIpv6Button.Click += async (s, e) => await ApplyIpv6SettingAsync();

        var gameModeLabel = new Label
        {
            Text = "Игровой режим",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 1425),
            AutoSize = true
        };

        var gameModeDescriptionLabel = new Label
        {
            Text = "ArbuzTweaker читает состояние из реестра и применяет выбранные значения. Для HKLM-параметров нужен запуск от имени администратора.",
            Location = new Point(20, 1455),
            MaximumSize = new Size(920, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var applyRecommendedGameTweaksButton = new Button
        {
            Text = "Отметить рекомендованные",
            Location = new Point(20, 1515),
            Size = new Size(210, 35)
        };
        applyRecommendedGameTweaksButton.Click += (s, e) => SetRecommendedGameTweaksChecked();

        var applyGameTweaksButton = new Button
        {
            Text = "Применить игровые твики",
            Location = new Point(240, 1515),
            Size = new Size(210, 35)
        };
        applyGameTweaksButton.Click += async (s, e) => await ApplyGameTweaksAsync();

        _gameTweaksPanel = new Panel
        {
            Location = new Point(20, 1570),
            Size = new Size(920, 560),
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(35, 35, 35)
        };
        PopulateGameTweaksPanel();

        _statusLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(20, 2150),
            AutoSize = true,
            ForeColor = Color.Green
        };

        Controls.Add(titleLabel);
        Controls.Add(infoLabel);
        Controls.Add(memoryLabel);
        Controls.Add(_nduCheckBox);
        Controls.Add(nduDescriptionLabel);
        Controls.Add(_nduStateLabel);
        Controls.Add(applyNduButton);
        Controls.Add(edgeLabel);
        Controls.Add(_edgeStartupBoostCheckBox);
        Controls.Add(edgeDescriptionLabel);
        Controls.Add(_edgeStateLabel);
        Controls.Add(applyEdgeButton);
        Controls.Add(repairNetworkLabel);
        Controls.Add(repairNetworkDescriptionLabel);
        Controls.Add(repairNetworkButton);
        Controls.Add(restartAdaptersLabel);
        Controls.Add(restartAdaptersDescriptionLabel);
        Controls.Add(restartAdaptersButton);
        Controls.Add(stabilityLabel);
        Controls.Add(_dhcpMediaSenseCheckBox);
        Controls.Add(dhcpMediaSenseDescriptionLabel);
        Controls.Add(_dhcpMediaSenseStateLabel);
        Controls.Add(applyDhcpMediaSenseButton);
        Controls.Add(_googleDnsCheckBox);
        Controls.Add(googleDnsDescriptionLabel);
        Controls.Add(_googleDnsStateLabel);
        Controls.Add(applyGoogleDnsButton);
        Controls.Add(_disableIpv6CheckBox);
        Controls.Add(ipv6DescriptionLabel);
        Controls.Add(_ipv6StateLabel);
        Controls.Add(applyIpv6Button);
        Controls.Add(gameModeLabel);
        Controls.Add(gameModeDescriptionLabel);
        Controls.Add(applyRecommendedGameTweaksButton);
        Controls.Add(applyGameTweaksButton);
        Controls.Add(_gameTweaksPanel);
        Controls.Add(_statusLabel);
    }

    private void LoadState()
    {
        LoadNduState();
        LoadDhcpMediaSenseState();
        LoadGoogleDnsState();
        LoadIpv6State();
        LoadEdgeState();
        LoadGameTweaksState();
    }

    private void LoadNduState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(NduRegistryPath, false);
            var currentValue = key?.GetValue("Start");
            if (currentValue is int startValue)
            {
                _nduCheckBox.Checked = startValue == 4;
                _nduStateLabel.Text = $"Текущее значение Ndu Start: {startValue}";
                _nduStateLabel.ForeColor = Color.Gainsboro;
                return;
            }

            _nduStateLabel.Text = "Не удалось прочитать значение Ndu Start";
            _nduStateLabel.ForeColor = Color.Orange;
        }
        catch
        {
            _nduStateLabel.Text = "Нет доступа к чтению Ndu из реестра";
            _nduStateLabel.ForeColor = Color.Orange;
        }
    }

    private void LoadDhcpMediaSenseState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(TcpipParametersRegistryPath, false);
            var currentValue = key?.GetValue("DisableDHCPMediaSense");
            if (currentValue is int value)
            {
                _dhcpMediaSenseCheckBox.Checked = value == 1;
                _dhcpMediaSenseStateLabel.Text = $"Текущее значение DisableDHCPMediaSense: {value}";
                _dhcpMediaSenseStateLabel.ForeColor = Color.Gainsboro;
                return;
            }

            _dhcpMediaSenseCheckBox.Checked = false;
            _dhcpMediaSenseStateLabel.Text = "DisableDHCPMediaSense не задан. Будет создан при применении.";
            _dhcpMediaSenseStateLabel.ForeColor = Color.Gray;
        }
        catch
        {
            _dhcpMediaSenseStateLabel.Text = "Нет доступа к чтению DisableDHCPMediaSense";
            _dhcpMediaSenseStateLabel.ForeColor = Color.Orange;
        }
    }

    private void LoadGoogleDnsState()
    {
        try
        {
            var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            if (activeAdapters.Count == 0)
            {
                _googleDnsCheckBox.Checked = false;
                _googleDnsStateLabel.Text = "Не найдено активных сетевых адаптеров";
                _googleDnsStateLabel.ForeColor = Color.Gray;
                return;
            }

            var allGoogleDns = true;
            foreach (var adapter in activeAdapters)
            {
                var dnsAddresses = adapter.GetIPProperties().DnsAddresses
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(address => address.ToString())
                    .ToList();

                if (!(dnsAddresses.Contains("8.8.8.8") && dnsAddresses.Contains("8.8.4.4")))
                {
                    allGoogleDns = false;
                    break;
                }
            }

            _googleDnsCheckBox.Checked = allGoogleDns;
            _googleDnsStateLabel.Text = allGoogleDns
                ? "Google DNS задан на активных адаптерах"
                : "Google DNS не задан на всех активных адаптерах";
            _googleDnsStateLabel.ForeColor = allGoogleDns ? Color.Gainsboro : Color.Gray;
        }
        catch
        {
            _googleDnsStateLabel.Text = "Не удалось определить состояние DNS";
            _googleDnsStateLabel.ForeColor = Color.Orange;
        }
    }

    private void LoadIpv6State()
    {
        try
        {
            var output = RunPowerShellQuery(
                "Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { (Get-NetAdapterBinding -Name $_.Name -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue).Enabled }");

            var values = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => string.Equals(value, "True", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "False", StringComparison.OrdinalIgnoreCase))
                .Select(value => string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (values.Count == 0)
            {
                _disableIpv6CheckBox.Checked = false;
                _ipv6StateLabel.Text = "Не удалось определить состояние IPv6";
                _ipv6StateLabel.ForeColor = Color.Gray;
                return;
            }

            var disabledEverywhere = values.All(value => !value);
            _disableIpv6CheckBox.Checked = disabledEverywhere;
            _ipv6StateLabel.Text = disabledEverywhere
                ? "IPv6 отключен на всех физических адаптерах"
                : "IPv6 включен хотя бы на одном физическом адаптере";
            _ipv6StateLabel.ForeColor = disabledEverywhere ? Color.Gainsboro : Color.Gray;
        }
        catch
        {
            _ipv6StateLabel.Text = "Не удалось определить состояние IPv6";
            _ipv6StateLabel.ForeColor = Color.Orange;
        }
    }

    private void LoadEdgeState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(EdgePolicyRegistryPath, false);
            var currentValue = key?.GetValue("StartupBoostEnabled");
            if (currentValue is int startupBoostValue)
            {
                _edgeStartupBoostCheckBox.Checked = startupBoostValue == 0;
                _edgeStateLabel.Text = $"Текущее значение StartupBoostEnabled: {startupBoostValue}";
                _edgeStateLabel.ForeColor = Color.Gainsboro;
                return;
            }

            _edgeStartupBoostCheckBox.Checked = false;
            _edgeStateLabel.Text = "StartupBoostEnabled не задан. Будет создан при применении.";
            _edgeStateLabel.ForeColor = Color.Gray;
        }
        catch
        {
            _edgeStateLabel.Text = "Нет доступа к чтению политики Edge из реестра";
            _edgeStateLabel.ForeColor = Color.Orange;
        }
    }

    private void PopulateGameTweaksPanel()
    {
        _gameTweakCheckBoxes.Clear();
        _gameTweaksPanel.Controls.Clear();

        var y = 12;
        foreach (var tweak in GameTweaks)
        {
            var checkBox = new CheckBox
            {
                Text = tweak.Name,
                Location = new Point(16, y),
                Size = new Size(360, 24),
                AutoSize = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            var descriptionLabel = new Label
            {
                Text = tweak.Description,
                Location = new Point(390, y + 2),
                Size = new Size(500, 42),
                AutoSize = false,
                ForeColor = Color.Gainsboro,
                BackColor = Color.Transparent
            };

            _gameTweakCheckBoxes[tweak] = checkBox;
            _gameTweaksPanel.Controls.Add(checkBox);
            _gameTweaksPanel.Controls.Add(descriptionLabel);
            y += 52;
        }
    }

    private void LoadGameTweaksState()
    {
        foreach (var (tweak, checkBox) in _gameTweakCheckBoxes)
        {
            checkBox.Checked = IsGameTweakEnabled(tweak);
        }
    }

    private static bool IsGameTweakEnabled(RegistryGameTweak tweak)
    {
        foreach (var value in tweak.Values)
        {
            using var key = OpenRegistryKey(value.Root, value.KeyPath, false);
            if (key?.GetValue(value.Name) is not int currentValue || currentValue != value.EnabledValue)
                return false;
        }

        return true;
    }

    private void SetRecommendedGameTweaksChecked()
    {
        foreach (var (tweak, checkBox) in _gameTweakCheckBoxes)
        {
            checkBox.Checked = tweak.Recommended;
        }

        ShowStatus("Отмечены рекомендованные игровые твики", Color.Green);
    }

    private async Task ApplyGameTweaksAsync()
    {
        var hasMachineTweaks = _gameTweakCheckBoxes.Any(pair => pair.Value.Checked != IsGameTweakEnabled(pair.Key)
            && pair.Key.Values.Any(value => value.Root == RegistryRoot.LocalMachine));

        if (hasMachineTweaks && MessageBox.Show(
            "Часть выбранных игровых твиков меняет HKLM и требует запуск от имени администратора. Продолжить применение?",
            "Игровые твики",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var applied = 0;
        var failed = 0;

        foreach (var (tweak, checkBox) in _gameTweakCheckBoxes)
        {
            try
            {
                ApplyGameTweak(tweak, checkBox.Checked);
                applied++;
            }
            catch (UnauthorizedAccessException)
            {
                failed++;
            }
            catch
            {
                failed++;
            }
        }

        LoadGameTweaksState();
        ShowStatus(failed == 0 ? $"Игровые твики применены: {applied}" : $"Применено: {applied}, ошибок: {failed}", failed == 0 ? Color.Green : Color.Orange);
        await Task.CompletedTask;
    }

    private static void ApplyGameTweak(RegistryGameTweak tweak, bool enabled)
    {
        foreach (var value in tweak.Values)
        {
            using var key = CreateRegistryKey(value.Root, value.KeyPath);
            if (key == null)
                continue;

            if (enabled)
            {
                key.SetValue(value.Name, value.EnabledValue, RegistryValueKind.DWord);
                continue;
            }

            if (value.DisabledValue.HasValue)
                key.SetValue(value.Name, value.DisabledValue.Value, RegistryValueKind.DWord);
            else if (key.GetValue(value.Name) != null)
                key.DeleteValue(value.Name, false);
        }
    }

    private static RegistryKey? OpenRegistryKey(RegistryRoot root, string keyPath, bool writable)
    {
        return root == RegistryRoot.CurrentUser
            ? Registry.CurrentUser.OpenSubKey(keyPath, writable)
            : Registry.LocalMachine.OpenSubKey(keyPath, writable);
    }

    private static RegistryKey? CreateRegistryKey(RegistryRoot root, string keyPath)
    {
        return root == RegistryRoot.CurrentUser
            ? Registry.CurrentUser.CreateSubKey(keyPath, true)
            : Registry.LocalMachine.CreateSubKey(keyPath, true);
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
            _nduStateLabel.Text = $"Текущее значение Ndu Start: {targetValue}";
            _nduStateLabel.ForeColor = Color.Gainsboro;
            ShowStatus("Твик Ndu применён", Color.Green);
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

    private async Task RepairNetworkAsync()
    {
        if (!ConfirmNetworkOperation("Будут выполнены стандартные команды Windows для восстановления сети."))
            return;

        ShowStatus("Выполняется восстановление сети...", Color.Gray);

        var commandResult = await RunElevatedCmdAsync(
            "ipconfig /flushdns & ipconfig /release & ipconfig /renew & netsh winsock reset & netsh int ip reset & netsh winhttp reset proxy");

        if (commandResult == ElevatedCommandResult.Success)
        {
            MessageBox.Show(
                "Команды восстановления сети выполнены. Если проблема не исчезнет сразу, рекомендуется перезагрузить компьютер.",
                "Исправить проблемы сети",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            ShowStatus("Восстановление сети выполнено", Color.Green);
            LoadGoogleDnsState();
            LoadIpv6State();
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        ShowStatus("Не удалось выполнить команды восстановления сети", Color.Orange);
    }

    private async Task ApplyDhcpMediaSenseAsync()
    {
        if (!ConfirmNetworkOperation("Будет изменён параметр DisableDHCPMediaSense в TCP/IP."))
            return;

        var targetValue = _dhcpMediaSenseCheckBox.Checked ? 1 : 0;

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(TcpipParametersRegistryPath, true);
            if (key == null)
            {
                ShowStatus("Не удалось открыть раздел TCP/IP Parameters", Color.Orange);
                return;
            }

            key.SetValue("DisableDHCPMediaSense", targetValue, RegistryValueKind.DWord);
            _dhcpMediaSenseStateLabel.Text = $"Текущее значение DisableDHCPMediaSense: {targetValue}";
            _dhcpMediaSenseStateLabel.ForeColor = Color.Gainsboro;
            ShowStatus("DisableDHCPMediaSense применён", Color.Green);
        }
        catch (UnauthorizedAccessException)
        {
            ShowStatus("Нужен запуск от имени администратора", Color.Orange);
        }
        catch
        {
            ShowStatus("Не удалось изменить DisableDHCPMediaSense", Color.Orange);
        }

        await Task.CompletedTask;
    }

    private async Task ApplyGoogleDnsAsync()
    {
        if (!ConfirmNetworkOperation("Будут изменены DNS-серверы на активных физических адаптерах."))
            return;

        var commandResult = _googleDnsCheckBox.Checked
            ? await RunElevatedPowerShellAsync("$adapters = Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object {$_.Status -eq 'Up'}; if(-not $adapters){ exit 2 }; foreach($adapter in $adapters){ Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses @('8.8.8.8','8.8.4.4') -ErrorAction Stop }")
            : await RunElevatedPowerShellAsync("$adapters = Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object {$_.Status -eq 'Up'}; if(-not $adapters){ exit 2 }; foreach($adapter in $adapters){ Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ResetServerAddresses -ErrorAction Stop }");

        if (commandResult == ElevatedCommandResult.Success)
        {
            LoadGoogleDnsState();
            ShowStatus("Настройки DNS обновлены", Color.Green);
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        ShowStatus("Не удалось изменить DNS-серверы", Color.Orange);
    }

    private async Task ApplyIpv6SettingAsync()
    {
        if (!ConfirmNetworkOperation("Будет изменена привязка IPv6 на физических адаптерах."))
            return;

        var script = _disableIpv6CheckBox.Checked
            ? "Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { Disable-NetAdapterBinding -Name $_.Name -ComponentID ms_tcpip6 -Confirm:$false -ErrorAction Stop }"
            : "Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { Enable-NetAdapterBinding -Name $_.Name -ComponentID ms_tcpip6 -Confirm:$false -ErrorAction Stop }";

        var commandResult = await RunElevatedPowerShellAsync(script);
        if (commandResult == ElevatedCommandResult.Success)
        {
            LoadIpv6State();
            ShowStatus("Настройка IPv6 обновлена", Color.Green);
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        ShowStatus("Не удалось изменить настройку IPv6", Color.Orange);
    }

    private async Task RestartNetworkAdaptersAsync()
    {
        if (!ConfirmNetworkOperation("Будут временно отключены и заново включены активные физические сетевые адаптеры."))
            return;

        var commandResult = await RunElevatedPowerShellAsync(
            "$adapters = Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object {$_.Status -eq 'Up'}; if(-not $adapters){ exit 2 }; foreach($adapter in $adapters){ Disable-NetAdapter -Name $adapter.Name -Confirm:$false -ErrorAction Stop }; Start-Sleep -Seconds 2; foreach($adapter in $adapters){ Enable-NetAdapter -Name $adapter.Name -Confirm:$false -ErrorAction Stop }");

        if (commandResult == ElevatedCommandResult.Success)
        {
            ShowStatus("Сетевые адаптеры перезапущены", Color.Green);
            LoadGoogleDnsState();
            LoadIpv6State();
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        ShowStatus("Не удалось перезапустить сетевые адаптеры", Color.Orange);
    }

    private async Task ApplyEdgeStartupBoostAsync()
    {
        var targetValue = _edgeStartupBoostCheckBox.Checked ? 0 : 1;

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(EdgePolicyRegistryPath, true);
            if (key == null)
            {
                ShowStatus("Не удалось открыть политику Edge", Color.Orange);
                return;
            }

            key.SetValue("StartupBoostEnabled", targetValue, RegistryValueKind.DWord);
            _edgeStateLabel.Text = $"Текущее значение StartupBoostEnabled: {targetValue}";
            _edgeStateLabel.ForeColor = Color.Gainsboro;
            ShowStatus("Настройка Edge Startup Boost применена", Color.Green);
        }
        catch (UnauthorizedAccessException)
        {
            ShowStatus("Нужен запуск от имени администратора", Color.Orange);
        }
        catch
        {
            ShowStatus("Не удалось изменить StartupBoostEnabled", Color.Orange);
        }

        await Task.CompletedTask;
    }

    private static bool ConfirmNetworkOperation(string details)
    {
        return MessageBox.Show(
            details + "\n\nЭто может временно разорвать текущее соединение. Продолжить?",
            "Сетевой твик",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private async Task<ElevatedCommandResult> RunElevatedCmdAsync(string command)
    {
        return await RunElevatedProcessAsync("cmd.exe", "/c " + command);
    }

    private async Task<ElevatedCommandResult> RunElevatedPowerShellAsync(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return await RunElevatedProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}");
    }

    private async Task<ElevatedCommandResult> RunElevatedProcessAsync(string fileName, string arguments)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0 ? ElevatedCommandResult.Success : ElevatedCommandResult.Failure;
            }
            catch (Win32Exception)
            {
                return ElevatedCommandResult.Cancelled;
            }
            catch
            {
                return ElevatedCommandResult.Failure;
            }
        });
    }

    private string RunPowerShellQuery(string script)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private async void ShowStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        await Task.Delay(2500);
        _statusLabel.Text = string.Empty;
    }

    private sealed record RegistryGameTweak(
        string Name,
        string Description,
        bool Recommended,
        IReadOnlyList<RegistryGameValue> Values);

    private sealed record RegistryGameValue(
        RegistryRoot Root,
        string KeyPath,
        string Name,
        int EnabledValue,
        int? DisabledValue)
    {
        public static RegistryGameValue CurrentUser(string keyPath, string name, int enabledValue, int? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.CurrentUser, keyPath, name, enabledValue, disabledValue);
        }

        public static RegistryGameValue LocalMachine(string keyPath, string name, int enabledValue, int? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.LocalMachine, keyPath, name, enabledValue, disabledValue);
        }
    }

    private enum RegistryRoot
    {
        CurrentUser,
        LocalMachine
    }

    private enum ElevatedCommandResult
    {
        Success,
        Cancelled,
        Failure
    }
}
