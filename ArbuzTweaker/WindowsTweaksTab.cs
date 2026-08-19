using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
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
    private const string DwmRegistryPath = @"SOFTWARE\Microsoft\Windows\Dwm";
    private const string NvidiaOverlayTaskFolderName = "ArbuzTweaker";
    private const string NvidiaOverlayRestartTaskName = "Restart NVIDIA Overlay";
    private const string NvidiaOverlayProcessRestartTaskName = "Restart NVIDIA Overlay for selected apps";
    private const string GameRealtimePriorityTaskName = "Set Dota 2 and SCP SL to realtime priority";
    private const string NvidiaOverlayExePath = @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe";
    private const string NvidiaOverlayHelperPath = @"C:\Program Files\NVIDIA Corporation\NVIDIA App\ShadowPlay\nvsphelper64.exe";
    private const int PowerShellQueryTimeoutMilliseconds = 8000;
    private const int ElevatedCommandTimeoutMilliseconds = 120000;
    private const int WindowsPageContentWidth = 820;

    private readonly AppSettingsService _appSettingsService;
    private readonly Dota2Service _dota2Service;
    private readonly ScpSlService _scpSlService;
    private readonly RegistryBackupService? _registryBackupService;

    private CheckBox _nduCheckBox = null!;
    private CheckBox _dhcpMediaSenseCheckBox = null!;
    private CheckBox _googleDnsCheckBox = null!;
    private CheckBox _disableIpv6CheckBox = null!;
    private CheckBox _edgeStartupBoostCheckBox = null!;
    private CheckBox _mpoDisabledCheckBox = null!;
    private CheckBox _nvidiaOverlayRestartCheckBox = null!;
    private CheckBox _nvidiaOverlayLaunchDotaCheckBox = null!;
    private CheckBox _nvidiaOverlayLaunchScpSlCheckBox = null!;
    private CheckBox _nvidiaOverlayLaunchCustomCheckBox = null!;
    private CheckBox _gameRealtimePriorityCheckBox = null!;
    private TextBox _nvidiaOverlayCustomProgramTextBox = null!;
    private Panel _gameTweaksPanel = null!;
    private Label _nduStateLabel = null!;
    private Label _dhcpMediaSenseStateLabel = null!;
    private Label _googleDnsStateLabel = null!;
    private Label _ipv6StateLabel = null!;
    private Label _edgeStateLabel = null!;
    private Label _mpoStateLabel = null!;
    private Label _nvidiaOverlayStateLabel = null!;
    private Label _nvidiaOverlayPreLaunchStateLabel = null!;
    private Label _gameRealtimePriorityStateLabel = null!;
    private Label _statusLabel = null!;
    private bool _isSynchronizingUnsafeControls;
    private int _statusMessageVersion;
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
                RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1)
            }),
        new(
            "Классический полноэкранный режим",
            "Отключает Fullscreen Optimizations через GameConfigStore: игры в полном экране работают в классическом эксклюзивном режиме.",
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
            })
        {
            Impact = UiTheme.Impact.AntiStutter
        },
        new(
            "Режим низкой задержки DWM",
            "Ставит UseOLEDTaskMode=1 в DWM. Управление MPO вынесено в системную вкладку Windows.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "UseOLEDTaskMode", 1, 0) }),
        new(
            "Выгрузка неиспользуемых DLL",
            "Добавляет AlwaysUnloadDll для Explorer в HKCU и HKLM. При выключении параметр удаляется.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "AlwaysUnloadDll", 1, null),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "AlwaysUnloadDll", 1, null)
            })
        {
            Impact = UiTheme.Impact.AntiStutter
        },
        new(
            "Отключение Game DVR",
            "Отключает встроенную запись игрового процесса Windows Game DVR.",
            true,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, 1),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AllowGameDVR", 0, 1)
            })
        {
            Impact = UiTheme.Impact.AntiStutter
        },
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
            new[] { RegistryGameValue.LocalMachine(@"System\CurrentControlSet\Services\USB", "DisableSelectiveSuspend", 1, 0) })
        {
            Impact = UiTheme.Impact.AntiStutter
        },
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
            "Отключение записи Xbox Game Bar",
            "Выключает AppCapture/GameDVR, захват звука, микрофона, курсора и часть интерфейса Xbox Game Bar. Не удаляет Xbox-компоненты и не трогает службы.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUser(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "CursorCaptureEnabled", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "MicrophoneCaptureEnabled", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\GameBar", "ShowStartupPanel", 0, 1),
                RegistryGameValue.CurrentUser(@"SOFTWARE\Microsoft\GameBar", "UseNexusForGameBarInterface", 0, 1)
            })
        {
            Impact = UiTheme.Impact.AntiStutter
        },
        new(
            "[Экспериментально] Низкая задержка Win32-презентации",
            "Включает Win32LowLatencyPresentationEnabled для текущего пользователя. Может снижать задержку в оконном и безрамочном режиме.",
            false,
            new[] { RegistryGameValue.CurrentUser(@"System\GameConfigStore", "Win32LowLatencyPresentationEnabled", 1, null) }),
        new(
            "[Экспериментально] Профиль MMCSS для игр",
            "Выставляет высокий профиль Tasks\\Games для планировщика мультимедиа (MMCSS). Может помочь приоритету игр, но эффект зависит от системы.",
            false,
            new[]
            {
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Affinity", 0, null),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "BackgroundPriority", 0, null),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Clock Rate", 10000, null),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8, 2),
                RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Io Priority", 4, null),
                RegistryGameValue.LocalMachineString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High", "Medium"),
                RegistryGameValue.LocalMachineString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", "High", "Normal"),
                RegistryGameValue.LocalMachineString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Latency Sensitive", "True", "False"),
                RegistryGameValue.LocalMachineString(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Background Only", "False", null)
            }),
        new(
            "[Экспериментально] Глобальные запросы таймера",
            "Разрешает GlobalTimerResolutionRequests в ядре Windows. Иногда снижает микрозадержки, но может увеличить расход батареи.",
            false,
            new[] { RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "GlobalTimerResolutionRequests", 1, 0) }),
        new(
            "[Экспериментально] Распределение таймеров ядра",
            "Включает DistributeTimers — распределение таймеров ядра по ядрам процессора. Не разгон и не отключение защиты, но проверять стоит отдельно от других твиков.",
            false,
            new[] { RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "DistributeTimers", 1, 0) }),
        new(
            "[Экспериментально] Очередь кадров DWM",
            "Ставит MaxQueuedBuffers=2 для DWM. Может повлиять на задержку в оконном и безрамочном режиме, эффект зависит от драйвера.",
            false,
            new[] { RegistryGameValue.CurrentUser(@"Software\Microsoft\Windows\DWM", "MaxQueuedBuffers", 2, null) }),
        new(
            "Отключение ускорения мыши",
            "Отключает Enhanced Pointer Precision через Control Panel\\Mouse (MouseSpeed и пороги в 0). Применяется после повторного входа в систему.",
            true,
            new[]
            {
                RegistryGameValue.CurrentUserString(@"Control Panel\Mouse", "MouseSpeed", "0", "1"),
                RegistryGameValue.CurrentUserString(@"Control Panel\Mouse", "MouseThreshold1", "0", "6"),
                RegistryGameValue.CurrentUserString(@"Control Panel\Mouse", "MouseThreshold2", "0", "10")
            }),
        new(
            "Ускорение работы видеокарты DWM",
            "Ставит EnableHWAcceleration=1 в DWM.",
            true,
            new[] { RegistryGameValue.LocalMachine(@"SOFTWARE\Microsoft\Windows\Dwm", "EnableHWAcceleration", 1, 0) })
    };

    public WindowsTweaksTab()
        : this(new AppSettingsService(new ConfigService()), new Dota2Service(), new ScpSlService(), null)
    {
    }

    public WindowsTweaksTab(AppSettingsService appSettingsService)
        : this(appSettingsService, new Dota2Service(), new ScpSlService(), null)
    {
    }

    public WindowsTweaksTab(
        AppSettingsService appSettingsService,
        Dota2Service dota2Service,
        ScpSlService scpSlService,
        RegistryBackupService? registryBackupService = null)
    {
        _appSettingsService = appSettingsService;
        _dota2Service = dota2Service;
        _scpSlService = scpSlService;
        _registryBackupService = registryBackupService;
        InitializeComponent();
        LoadStateAsync();
    }

    private void InitializeComponent()
    {
        AutoScroll = false;
        BackColor = UiTheme.Surface;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.Surface
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };
        UiTheme.StyleTabControl(tabControl);

        var systemPage = new TabPage
        {
            Text = "Система",
            BackColor = UiTheme.Surface,
            ForeColor = Color.White,
            AutoScroll = true
        };

        var gameModePage = new TabPage
        {
            Text = "Игровой режим",
            BackColor = UiTheme.Surface,
            ForeColor = Color.White,
            AutoScroll = true
        };

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
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = UiTheme.TextMuted
        };

        var adminStatusLabel = CreateAdminStatusLabel();

        var memoryLabel = new Label
        {
            Text = "Память и фоновые процессы",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 315),
            AutoSize = true
        };

        _nduCheckBox = new CheckBox
        {
            Text = UnsafeTweaksPrompt.Marker + " Устранение сетевой утечки (Ndu)",
            Location = new Point(20, 345),
            AutoSize = true,
            ForeColor = Color.White
        };

        var nduDescriptionLabel = new Label
        {
            Text = "Меняет значение Start у службы Ndu. При включении ставит 4, что может снизить рост потребления памяти из-за Ndu.",
            Location = new Point(20, 372),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _nduStateLabel = new Label
        {
            Text = "Текущее значение Ndu Start: неизвестно",
            Location = new Point(20, 418),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyNduButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 448),
            Size = new Size(120, 35)
        };
        applyNduButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyNduSettingAsync);

        var edgeLabel = new Label
        {
            Text = "Microsoft Edge",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 510),
            AutoSize = true
        };

        _edgeStartupBoostCheckBox = new CheckBox
        {
            Text = "Отключить Edge Startup Boost",
            Location = new Point(20, 540),
            AutoSize = true,
            ForeColor = Color.White
        };

        var edgeDescriptionLabel = new Label
        {
            Text = "Создаёт или меняет параметр StartupBoostEnabled в политике Edge. При включении ставит 0, чтобы Edge не подгружался в фоне заранее.",
            Location = new Point(20, 567),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _edgeStateLabel = new Label
        {
            Text = "Текущее значение StartupBoostEnabled: неизвестно",
            Location = new Point(20, 613),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyEdgeButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 643),
            Size = new Size(120, 35)
        };
        applyEdgeButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyEdgeStartupBoostAsync);

        var repairNetworkLabel = new Label
        {
            Text = "Быстрое восстановление сети",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 710),
            AutoSize = true
        };

        var repairNetworkDescriptionLabel = new Label
        {
            Text = "Запускает стандартные команды Windows для восстановления сетевого стека: сброс Winsock, TCP/IP, DNS-кэша, release/renew IP и сброс WinHTTP proxy. Это безопасный базовый набор, если интернет периодически пропадает.",
            Location = new Point(20, 740),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var repairNetworkButton = new Button
        {
            Text = "Исправить проблемы сети",
            Location = new Point(20, 802),
            Size = new Size(190, 35)
        };
        repairNetworkButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, RepairNetworkAsync);

        var restartAdaptersLabel = new Label
        {
            Text = "Перезапуск сетевого адаптера",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 875),
            AutoSize = true
        };

        var restartAdaptersDescriptionLabel = new Label
        {
            Text = "Отключает и заново включает все активные физические сетевые адаптеры. Полезно как быстрый ручной аналог переподключения кабеля или адаптера.",
            Location = new Point(20, 905),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        var restartAdaptersButton = new Button
        {
            Text = "Перезапустить адаптер",
            Location = new Point(20, 955),
            Size = new Size(180, 35)
        };
        restartAdaptersButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, RestartNetworkAdaptersAsync);

        var stabilityLabel = new Label
        {
            Text = "Стабильность подключения",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 1030),
            AutoSize = true
        };

        _dhcpMediaSenseCheckBox = new CheckBox
        {
            Text = UnsafeTweaksPrompt.Marker + " DisableDHCPMediaSense",
            Location = new Point(20, 1060),
            AutoSize = true,
            ForeColor = Color.White
        };

        var dhcpMediaSenseDescriptionLabel = new Label
        {
            Text = "Создаёт или меняет параметр DisableDHCPMediaSense в TCP/IP. Иногда помогает при обрывах проводного подключения, когда Windows слишком агрессивно реагирует на краткие потери линка.",
            Location = new Point(20, 1087),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _dhcpMediaSenseStateLabel = new Label
        {
            Text = "Текущее значение DisableDHCPMediaSense: неизвестно",
            Location = new Point(20, 1150),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyDhcpMediaSenseButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1180),
            Size = new Size(120, 35)
        };
        applyDhcpMediaSenseButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyDhcpMediaSenseAsync);

        _googleDnsCheckBox = new CheckBox
        {
            Text = "Использовать Google DNS (8.8.8.8 / 8.8.4.4)",
            Location = new Point(20, 1250),
            AutoSize = true,
            ForeColor = Color.White
        };

        var googleDnsDescriptionLabel = new Label
        {
            Text = "Назначает активным физическим адаптерам публичные DNS Google. Это может помочь, если проблема связана именно с резолвингом DNS, а не с самим подключением.",
            Location = new Point(20, 1277),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _googleDnsStateLabel = new Label
        {
            Text = "Состояние Google DNS: неизвестно",
            Location = new Point(20, 1340),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyGoogleDnsButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1370),
            Size = new Size(120, 35)
        };
        applyGoogleDnsButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyGoogleDnsAsync);

        _disableIpv6CheckBox = new CheckBox
        {
            Text = "Отключить IPv6 на активных физических адаптерах",
            Location = new Point(20, 1440),
            AutoSize = true,
            ForeColor = Color.White
        };

        var ipv6DescriptionLabel = new Label
        {
            Text = "Отключает IPv6 через привязку сетевого адаптера. Иногда помогает, если проблема вызвана конфликтами IPv4/IPv6 или странным поведением провайдера/роутера.",
            Location = new Point(20, 1467),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _ipv6StateLabel = new Label
        {
            Text = "Состояние IPv6: неизвестно",
            Location = new Point(20, 1530),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyIpv6Button = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1560),
            Size = new Size(120, 35)
        };
        applyIpv6Button.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyIpv6SettingAsync);

        var graphicsLabel = new Label
        {
            Text = "Графика Windows",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 105),
            AutoSize = true
        };

        _mpoDisabledCheckBox = new CheckBox
        {
            Text = "Отключить Multi-Plane Overlay (MPO)",
            Location = new Point(20, 135),
            AutoSize = true,
            ForeColor = Color.White
        };

        var mpoDescriptionLabel = new Label
        {
            Text = "Официальный workaround NVIDIA для мерцаний/артефактов: отключение ставит OverlayTestMode=5 в DWM, включение удаляет этот параметр.",
            Location = new Point(20, 162),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _mpoStateLabel = new Label
        {
            Text = "Состояние MPO: неизвестно",
            Location = new Point(20, 208),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyMpoButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 238),
            Size = new Size(120, 35)
        };
        applyMpoButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyMpoSettingAsync);

        var nvidiaOverlayLabel = new Label
        {
            Text = "NVIDIA Overlay",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 1635),
            AutoSize = true
        };

        _nvidiaOverlayRestartCheckBox = new CheckBox
        {
            Text = "Запускать NVIDIA Overlay при входе в Windows и выходе из сна, если он не запущен",
            Location = new Point(20, 1665),
            AutoSize = true,
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            ForeColor = Color.White
        };

        var nvidiaOverlayDescriptionLabel = new Label
        {
            Text = "Создаёт задачу планировщика Windows, которая проверяет NVIDIA Overlay.exe и запускает его только если он не работает. Уже запущенный Overlay и Мгновенный повтор не затрагиваются.",
            Location = new Point(20, 1692),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _nvidiaOverlayStateLabel = new Label
        {
            Text = "Состояние задачи NVIDIA Overlay: неизвестно",
            Location = new Point(20, 1740),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyNvidiaOverlayButton = new Button
        {
            Text = "Применить",
            Location = new Point(20, 1770),
            Size = new Size(120, 35)
        };
        applyNvidiaOverlayButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyNvidiaOverlayRestartAsync);

        var nvidiaOverlayPreLaunchLabel = new Label
        {
            Text = "Запуск NVIDIA Overlay для выбранных игр",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 1830),
            AutoSize = true
        };

        var nvidiaOverlayPreLaunchDescriptionLabel = new Label
        {
            Text = "Выберите игры или программу и нажмите Применить. При запуске выбранных .exe твикер проверит NVIDIA Overlay из NVIDIA App и запустит его только если он не работает. Уже запущенный Overlay и Мгновенный повтор не затрагиваются.",
            Location = new Point(20, 1860),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _nvidiaOverlayLaunchDotaCheckBox = new CheckBox
        {
            Text = "Dota 2",
            Location = new Point(20, 1910),
            AutoSize = true,
            ForeColor = Color.White
        };
        _nvidiaOverlayLaunchDotaCheckBox.CheckedChanged += (s, e) => SaveNvidiaOverlayPreLaunchSettings();

        _nvidiaOverlayLaunchScpSlCheckBox = new CheckBox
        {
            Text = "SCP:SL",
            Location = new Point(100, 1910),
            AutoSize = true,
            ForeColor = Color.White
        };
        _nvidiaOverlayLaunchScpSlCheckBox.CheckedChanged += (s, e) => SaveNvidiaOverlayPreLaunchSettings();

        _nvidiaOverlayLaunchCustomCheckBox = new CheckBox
        {
            Text = "Своя программа",
            Location = new Point(195, 1910),
            AutoSize = true,
            ForeColor = Color.White
        };
        _nvidiaOverlayLaunchCustomCheckBox.CheckedChanged += (s, e) => SaveNvidiaOverlayPreLaunchSettings();

        _nvidiaOverlayCustomProgramTextBox = new TextBox
        {
            Location = new Point(20, 1946),
            Size = new Size(650, 28),
            PlaceholderText = "Путь к .exe своей программы"
        };
        UiTheme.StyleSearchTextBox(_nvidiaOverlayCustomProgramTextBox);
        _nvidiaOverlayCustomProgramTextBox.TextChanged += (s, e) => SaveNvidiaOverlayPreLaunchSettings();

        var browseNvidiaOverlayCustomProgramButton = new Button
        {
            Text = "Обзор...",
            Location = new Point(685, 1944),
            Size = new Size(100, 32)
        };
        browseNvidiaOverlayCustomProgramButton.Click += (s, e) => SelectNvidiaOverlayCustomProgram();

        var applyNvidiaOverlayPreLaunchButton = new Button
        {
            Text = "Применить автозапуск",
            Location = new Point(20, 2020),
            Size = new Size(240, 35)
        };
        applyNvidiaOverlayPreLaunchButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyNvidiaOverlayPreLaunchAsync);

        _nvidiaOverlayPreLaunchStateLabel = new Label
        {
            Text = "Выберите, для каких .exe проверять и при необходимости запускать NVIDIA Overlay из NVIDIA App.",
            Location = new Point(20, 1992),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var gamePriorityLabel = new Label
        {
            Text = "Приоритет процессов игр",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true
        };

        _gameRealtimePriorityCheckBox = new CheckBox
        {
            Text = UnsafeTweaksPrompt.Marker + " Автоматически ставить Dota 2 и SCP:SL в приоритет реального времени",
            AutoSize = true,
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            ForeColor = Color.White
        };

        var gamePriorityDescriptionLabel = new Label
        {
            Text = "Создаёт задачу Планировщика Windows. При запуске найденной игры ей назначается приоритет реального времени до закрытия процесса. Требуются права администратора и аудит запуска процессов Windows. Реальное время может вызвать зависание системы, проблемы со звуком или вводом, поэтому используйте только при понимании риска.",
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        _gameRealtimePriorityStateLabel = new Label
        {
            Text = "Проверка задачи приоритета игр...",
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var applyGameRealtimePriorityButton = new Button
        {
            Text = "Применить",
            Size = new Size(120, 35)
        };
        applyGameRealtimePriorityButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyGameRealtimePriorityAsync);

        RegisterUnsafeCheckBox(_nduCheckBox);
        RegisterUnsafeCheckBox(_dhcpMediaSenseCheckBox);
        RegisterUnsafeCheckBox(_googleDnsCheckBox);
        RegisterUnsafeCheckBox(_disableIpv6CheckBox);
        RegisterUnsafeCheckBox(_gameRealtimePriorityCheckBox);

        UiTheme.StyleActionButton(applyMpoButton, true);
        UiTheme.StyleActionButton(applyNduButton, true);
        UiTheme.StyleActionButton(applyEdgeButton, true);
        UiTheme.StyleActionButton(repairNetworkButton, true);
        UiTheme.StyleActionButton(restartAdaptersButton, true);
        UiTheme.StyleActionButton(applyDhcpMediaSenseButton, true);
        UiTheme.StyleActionButton(applyGoogleDnsButton, true);
        UiTheme.StyleActionButton(applyIpv6Button, true);
        UiTheme.StyleActionButton(applyNvidiaOverlayButton, true);
        UiTheme.StyleActionButton(browseNvidiaOverlayCustomProgramButton);
        UiTheme.StyleActionButton(applyNvidiaOverlayPreLaunchButton, true);
        UiTheme.StyleActionButton(applyGameRealtimePriorityButton, true);

        var gameModeLabel = new Label
        {
            Text = "Игровой режим",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 10),
            AutoSize = true
        };

        var gameModeDescriptionLabel = new Label
        {
            Text = "Выберите профиль или отметьте пункты вручную. Безопасный профиль выбирает только рекомендованные HKCU-твики, игровой - все рекомендованные, экспериментальный - весь список. Для HKLM-параметров нужен запуск от имени администратора.",
            Location = new Point(20, 40),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };

        // Кнопки профилей авторазмерные: фиксированная ширина резала «Профиль: безопасный»
        // и «Профиль: экспериментальный» до первого слова.
        var safeProfileButton = new Button
        {
            Text = "Профиль: безопасный",
            Location = new Point(20, 95),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 35),
            Padding = new Padding(10, 0, 10, 0)
        };
        UiTheme.StyleActionButton(safeProfileButton);
        safeProfileButton.Click += (s, e) => SetSafeGameTweaksChecked();

        var gameProfileButton = new Button
        {
            Text = "Профиль: игровой",
            Location = new Point(220, 95),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 35),
            Padding = new Padding(10, 0, 10, 0)
        };
        UiTheme.StyleActionButton(gameProfileButton);
        gameProfileButton.Click += (s, e) => SetRecommendedGameTweaksChecked();

        var experimentalProfileButton = new Button
        {
            Text = "Профиль: экспериментальный",
            Location = new Point(400, 95),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 35),
            Padding = new Padding(10, 0, 10, 0)
        };
        UiTheme.StyleActionButton(experimentalProfileButton);
        experimentalProfileButton.Click += (s, e) => SetExperimentalGameTweaksChecked();

        var applyGameTweaksButton = new Button
        {
            Text = "Применить игровые твики",
            Location = new Point(20, 145),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 35),
            Padding = new Padding(10, 0, 10, 0)
        };
        UiTheme.StyleActionButton(applyGameTweaksButton, true);
        applyGameTweaksButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyGameTweaksAsync);

        var restoreRegistryBackupButton = new Button
        {
            Text = "Откатить бэкап реестра",
            Location = new Point(240, 145),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 35),
            Padding = new Padding(10, 0, 10, 0)
        };
        UiTheme.StyleActionButton(restoreRegistryBackupButton);
        restoreRegistryBackupButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, RestoreRegistryBackupAsync);

        _gameTweaksPanel = new Panel
        {
            Location = new Point(20, 200),
            Size = new Size(WindowsPageContentWidth, 1),
            AutoScroll = false,
            BorderStyle = BorderStyle.None,
            BackColor = UiTheme.Surface
        };
        UiTheme.EnableDoubleBuffering(_gameTweaksPanel);
        PopulateGameTweaksPanel();

        _statusLabel = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(20, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Green,
            Visible = false
        };

        var systemLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(20, 10, 20, 20),
            Margin = new Padding(0),
            BackColor = UiTheme.Surface
        };
        UiTheme.EnableDoubleBuffering(systemLayout);

        var systemCards = new List<(Panel Card, Control[] Children, Label Badge)>();

        // Каждый твик — карточка на тёмной подложке с маркером влияния справа вверху
        // (единый вид с вкладками «Игровой режим» и «Оптимизация»).
        Panel MakeCard(UiTheme.Impact impact, params Control[] children)
        {
            var card = UiTheme.CreateCard();
            foreach (var child in children)
                card.Controls.Add(child);

            var badge = UiTheme.CreateImpactBadge(impact);
            card.Controls.Add(badge);
            systemCards.Add((card, children, badge));
            return card;
        }

        void AddIntro(Control control, int bottomMargin)
        {
            control.Margin = new Padding(0, 0, 0, bottomMargin);
            systemLayout.Controls.Add(control);
        }

        void AddHeader(Label label, string? text = null)
        {
            if (text != null)
                label.Text = text;
            label.ForeColor = UiTheme.AccentGreen;
            label.Font = new Font("Segoe UI Semibold", 11F);
            label.AutoSize = true;
            label.Margin = new Padding(2, 12, 0, 6);
            systemLayout.Controls.Add(label);
        }

        void AddCard(Panel card)
        {
            card.Margin = new Padding(0, 0, 0, 12);
            systemLayout.Controls.Add(card);
        }

        void AsCardTitle(Label label)
        {
            label.ForeColor = UiTheme.TextPrimary;
            label.Font = new Font("Segoe UI Semibold", 10F);
            label.AutoSize = true;
        }

        var nvidiaOverlayLaunchChoicesPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        _nvidiaOverlayLaunchDotaCheckBox.Margin = new Padding(0, 0, 12, 0);
        _nvidiaOverlayLaunchScpSlCheckBox.Margin = new Padding(0, 0, 12, 0);
        _nvidiaOverlayLaunchCustomCheckBox.Margin = new Padding(0);
        nvidiaOverlayLaunchChoicesPanel.Controls.Add(_nvidiaOverlayLaunchDotaCheckBox);
        nvidiaOverlayLaunchChoicesPanel.Controls.Add(_nvidiaOverlayLaunchScpSlCheckBox);
        nvidiaOverlayLaunchChoicesPanel.Controls.Add(_nvidiaOverlayLaunchCustomCheckBox);

        var nvidiaOverlayCustomProgramPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        _nvidiaOverlayCustomProgramTextBox.Margin = new Padding(0, 0, 12, 0);
        browseNvidiaOverlayCustomProgramButton.Margin = new Padding(0);
        nvidiaOverlayCustomProgramPanel.Controls.Add(_nvidiaOverlayCustomProgramTextBox);
        nvidiaOverlayCustomProgramPanel.Controls.Add(browseNvidiaOverlayCustomProgramButton);

        AddIntro(titleLabel, 8);
        AddIntro(infoLabel, 10);
        AddIntro(adminStatusLabel, 6);

        AddHeader(graphicsLabel);
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, _mpoDisabledCheckBox, mpoDescriptionLabel, _mpoStateLabel, applyMpoButton));

        AddHeader(memoryLabel);
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, _nduCheckBox, nduDescriptionLabel, _nduStateLabel, applyNduButton));

        AddHeader(edgeLabel);
        AddCard(MakeCard(UiTheme.Impact.Background, _edgeStartupBoostCheckBox, edgeDescriptionLabel, _edgeStateLabel, applyEdgeButton));

        AddHeader(stabilityLabel, "Сеть");
        AsCardTitle(repairNetworkLabel);
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, repairNetworkLabel, repairNetworkDescriptionLabel, repairNetworkButton));
        AsCardTitle(restartAdaptersLabel);
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, restartAdaptersLabel, restartAdaptersDescriptionLabel, restartAdaptersButton));
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, _dhcpMediaSenseCheckBox, dhcpMediaSenseDescriptionLabel, _dhcpMediaSenseStateLabel, applyDhcpMediaSenseButton));
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, _googleDnsCheckBox, googleDnsDescriptionLabel, _googleDnsStateLabel, applyGoogleDnsButton));
        AddCard(MakeCard(UiTheme.Impact.AntiStutter, _disableIpv6CheckBox, ipv6DescriptionLabel, _ipv6StateLabel, applyIpv6Button));

        AddHeader(nvidiaOverlayLabel);
        AddCard(MakeCard(UiTheme.Impact.Background, _nvidiaOverlayRestartCheckBox, nvidiaOverlayDescriptionLabel, _nvidiaOverlayStateLabel, applyNvidiaOverlayButton));
        AsCardTitle(nvidiaOverlayPreLaunchLabel);
        AddCard(MakeCard(UiTheme.Impact.Background, nvidiaOverlayPreLaunchLabel, nvidiaOverlayPreLaunchDescriptionLabel, nvidiaOverlayLaunchChoicesPanel, nvidiaOverlayCustomProgramPanel, _nvidiaOverlayPreLaunchStateLabel, applyNvidiaOverlayPreLaunchButton));

        AddHeader(gamePriorityLabel);
        AddCard(MakeCard(UiTheme.Impact.Fps, _gameRealtimePriorityCheckBox, gamePriorityDescriptionLabel, _gameRealtimePriorityStateLabel, applyGameRealtimePriorityButton));

        systemPage.Controls.Add(systemLayout);

        // Интро-подписи переносятся по ширине; внутри карточек ширину и высоту считаем вручную.
        UiTheme.EnableDynamicLabelWrap(systemLayout, infoLabel, adminStatusLabel);

        void LayoutSystemCards()
        {
            // В свёрнутом окне не трогаем раскладку — иначе разворачивание моргает.
            if (FindForm() is { WindowState: FormWindowState.Minimized })
                return;

            var contentWidth = Math.Max(360, systemLayout.ClientSize.Width - systemLayout.Padding.Horizontal);

            foreach (var (card, children, badge) in systemCards)
            {
                card.Width = contentWidth;
                var innerWidth = contentWidth - 28;

                // Маркер влияния — в правом верхнем углу карточки.
                badge.Location = new Point(contentWidth - badge.Width - 14, 12);

                var y = 12;
                var first = true;

                foreach (var child in children)
                {
                    if (child is Label or CheckBox)
                    {
                        // Заголовок карточки не должен залезать под маркер.
                        var cap = first ? innerWidth - badge.Width - 12 : innerWidth;
                        child.MaximumSize = new Size(Math.Max(120, cap), 0);
                    }
                    else if (child is FlowLayoutPanel flow)
                    {
                        flow.MaximumSize = new Size(innerWidth, 0);
                        if (ReferenceEquals(child, nvidiaOverlayCustomProgramPanel))
                        {
                            _nvidiaOverlayCustomProgramTextBox.Width = Math.Max(
                                220,
                                innerWidth - browseNvidiaOverlayCustomProgramButton.Width
                                    - _nvidiaOverlayCustomProgramTextBox.Margin.Horizontal - 6);
                        }
                    }

                    child.Location = new Point(14, y);
                    y += child.Height + 8;
                    first = false;
                }

                card.Height = Math.Max(y + 4, badge.Bottom + 10);
            }
        }

        systemLayout.SizeChanged += (s, e) => LayoutSystemCards();
        LayoutSystemCards();

        gameModePage.Controls.Add(gameModeLabel);
        gameModePage.Controls.Add(gameModeDescriptionLabel);
        gameModePage.Controls.Add(safeProfileButton);
        gameModePage.Controls.Add(gameProfileButton);
        gameModePage.Controls.Add(experimentalProfileButton);
        gameModePage.Controls.Add(applyGameTweaksButton);
        gameModePage.Controls.Add(restoreRegistryBackupButton);
        gameModePage.Controls.Add(_gameTweaksPanel);

        // Описание с AutoSize растёт по высоте (особенно на узком окне и при увеличенном DPI)
        // и раньше налезало на кнопки профилей — ряды ниже позиционируются от его низа,
        // а список твиков подгоняется под реальную ширину страницы.
        void LayoutGameModePage()
        {
            // В свёрнутом окне не пересобираем список — иначе разворачивание моргает и сбрасывает прокрутку.
            if (FindForm() is { WindowState: FormWindowState.Minimized })
                return;

            var profileButtonsTop = gameModeDescriptionLabel.Bottom + 14;
            safeProfileButton.Top = profileButtonsTop;
            gameProfileButton.Top = profileButtonsTop;
            gameProfileButton.Left = safeProfileButton.Right + 10;

            // На узком окне третья кнопка не влезает в ряд — переносится на следующий.
            var availablePageWidth = Math.Max(360, gameModePage.ClientSize.Width - 24);
            if (gameProfileButton.Right + 10 + experimentalProfileButton.Width <= availablePageWidth)
            {
                experimentalProfileButton.Top = profileButtonsTop;
                experimentalProfileButton.Left = gameProfileButton.Right + 10;
            }
            else
            {
                experimentalProfileButton.Top = profileButtonsTop + safeProfileButton.Height + 8;
                experimentalProfileButton.Left = 20;
            }

            var actionButtonsTop = experimentalProfileButton.Bottom + 15;
            applyGameTweaksButton.Top = actionButtonsTop;
            restoreRegistryBackupButton.Top = actionButtonsTop;
            restoreRegistryBackupButton.Left = applyGameTweaksButton.Right + 10;

            var tweaksPanelWidth = Math.Max(560, gameModePage.ClientSize.Width - 44);
            if (_gameTweaksPanel.Width != tweaksPanelWidth)
            {
                _gameTweaksPanel.Width = tweaksPanelWidth;
                PopulateGameTweaksPanel();
            }

            _gameTweaksPanel.Top = actionButtonsTop + applyGameTweaksButton.Height + 20;
            gameModePage.AutoScrollMinSize = new Size(0, _gameTweaksPanel.Bottom + 24);
        }

        gameModeDescriptionLabel.SizeChanged += (s, e) => LayoutGameModePage();
        gameModePage.SizeChanged += (s, e) =>
        {
            gameModeDescriptionLabel.MaximumSize = new Size(
                Math.Max(360, gameModePage.ClientSize.Width - 60),
                0);
            LayoutGameModePage();
        };
        LayoutGameModePage();

        var pcTuningPage = new TabPage
        {
            Text = "Оптимизация",
            BackColor = UiTheme.Surface,
            ForeColor = Color.White
        };
        pcTuningPage.Controls.Add(new PcTuningTab(
            new PcTuningService(_registryBackupService),
            _registryBackupService == null ? null : RestoreRegistryBackupAsync));

        tabControl.TabPages.Add(systemPage);
        tabControl.TabPages.Add(gameModePage);
        tabControl.TabPages.Add(pcTuningPage);
        rootLayout.Controls.Add(tabControl, 0, 0);
        rootLayout.Controls.Add(_statusLabel, 0, 1);
        Controls.Add(rootLayout);
    }

    private async void LoadStateAsync()
    {
        _isSynchronizingUnsafeControls = true;
        try
        {
            LoadNduState();
            LoadDhcpMediaSenseState();
            LoadGoogleDnsState();
            LoadEdgeState();
            LoadMpoState();
            LoadNvidiaOverlayPreLaunchSettings();
            LoadGameTweaksState();

            _ipv6StateLabel.Text = "Определение состояния IPv6...";
            _ipv6StateLabel.ForeColor = Color.Gray;
            _nvidiaOverlayStateLabel.Text = "Проверка задачи NVIDIA Overlay...";
            _nvidiaOverlayStateLabel.ForeColor = Color.Gray;
            _gameRealtimePriorityStateLabel.Text = "Проверка задачи приоритета игр...";
            _gameRealtimePriorityStateLabel.ForeColor = Color.Gray;

            await Task.WhenAll(
                LoadIpv6StateAsync(),
                LoadNvidiaOverlayRestartStateAsync(),
                LoadGameRealtimePriorityStateAsync());
        }
        finally
        {
            _isSynchronizingUnsafeControls = false;
        }
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

    private async Task LoadIpv6StateAsync()
    {
        try
        {
            var output = await RunPowerShellQueryAsync(
                "Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { (Get-NetAdapterBinding -Name $_.Name -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue).Enabled }");

            if (IsDisposed || Disposing)
                return;

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

    private void LoadMpoState()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(DwmRegistryPath, false);
            var currentValue = key?.GetValue("OverlayTestMode");
            if (currentValue is int overlayTestMode)
            {
                _mpoDisabledCheckBox.Checked = overlayTestMode == 5;
                _mpoStateLabel.Text = overlayTestMode == 5
                    ? "MPO отключено: OverlayTestMode=5"
                    : $"OverlayTestMode задан нестандартно: {overlayTestMode}";
                _mpoStateLabel.ForeColor = overlayTestMode == 5 ? Color.Gainsboro : Color.Orange;
                return;
            }

            _mpoDisabledCheckBox.Checked = false;
            _mpoStateLabel.Text = "MPO включено: OverlayTestMode не задан";
            _mpoStateLabel.ForeColor = Color.Gray;
        }
        catch
        {
            _mpoDisabledCheckBox.Checked = false;
            _mpoStateLabel.Text = "Не удалось определить состояние MPO";
            _mpoStateLabel.ForeColor = Color.Orange;
        }
    }

    private async Task LoadNvidiaOverlayRestartStateAsync()
    {
        try
        {
            var output = await RunPowerShellQueryAsync(
                $@"$ErrorActionPreference = 'Stop'; $service = New-Object -ComObject 'Schedule.Service'; $service.Connect(); try {{ $folder = $service.GetFolder('\{NvidiaOverlayTaskFolderName}'); $task = $folder.GetTask('{NvidiaOverlayRestartTaskName}'); if($task.Enabled){{ 'ENABLED' }} else {{ 'DISABLED' }} }} catch {{ 'MISSING' }}");

            if (IsDisposed || Disposing)
                return;

            var state = output.Trim();
            var overlayAvailable = IsNvidiaOverlayAvailable();
            var overlayWarning = overlayAvailable ? string.Empty : " NVIDIA App не найдена по стандартным путям.";

            if (string.Equals(state, "ENABLED", StringComparison.OrdinalIgnoreCase))
            {
                _nvidiaOverlayRestartCheckBox.Checked = true;
                _nvidiaOverlayStateLabel.Text = "Задача NVIDIA Overlay включена." + overlayWarning;
                _nvidiaOverlayStateLabel.ForeColor = overlayAvailable ? Color.Gainsboro : Color.Orange;
                return;
            }

            if (string.Equals(state, "DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                _nvidiaOverlayRestartCheckBox.Checked = false;
                _nvidiaOverlayStateLabel.Text = "Задача NVIDIA Overlay есть, но отключена." + overlayWarning;
                _nvidiaOverlayStateLabel.ForeColor = Color.Orange;
                return;
            }

            _nvidiaOverlayRestartCheckBox.Checked = false;
            _nvidiaOverlayStateLabel.Text = "Автоперезапуск NVIDIA Overlay не настроен." + overlayWarning;
            _nvidiaOverlayStateLabel.ForeColor = overlayAvailable ? Color.Gray : Color.Orange;
        }
        catch
        {
            _nvidiaOverlayRestartCheckBox.Checked = false;
            _nvidiaOverlayStateLabel.Text = "Не удалось определить состояние задачи NVIDIA Overlay";
            _nvidiaOverlayStateLabel.ForeColor = Color.Orange;
        }
    }

    private async Task LoadGameRealtimePriorityStateAsync()
    {
        try
        {
            var output = await RunPowerShellQueryAsync(
                $@"$ErrorActionPreference = 'Stop'; $service = New-Object -ComObject 'Schedule.Service'; $service.Connect(); try {{ $folder = $service.GetFolder('\{NvidiaOverlayTaskFolderName}'); $task = $folder.GetTask('{GameRealtimePriorityTaskName}'); if($task.Enabled){{ 'ENABLED' }} else {{ 'DISABLED' }} }} catch {{ 'MISSING' }}");

            if (IsDisposed || Disposing)
                return;

            var state = output.Trim();
            if (string.Equals(state, "ENABLED", StringComparison.OrdinalIgnoreCase))
            {
                _gameRealtimePriorityCheckBox.Checked = true;
                _gameRealtimePriorityStateLabel.Text = "Автоматическое назначение приоритета реального времени включено.";
                _gameRealtimePriorityStateLabel.ForeColor = Color.Orange;
                return;
            }

            _gameRealtimePriorityCheckBox.Checked = false;
            _gameRealtimePriorityStateLabel.Text = string.Equals(state, "DISABLED", StringComparison.OrdinalIgnoreCase)
                ? "Задача приоритета игр существует, но отключена."
                : "Автоматическое назначение приоритета реального времени не настроено.";
            _gameRealtimePriorityStateLabel.ForeColor = Color.Gray;
        }
        catch
        {
            _gameRealtimePriorityCheckBox.Checked = false;
            _gameRealtimePriorityStateLabel.Text = "Не удалось определить состояние задачи приоритета игр.";
            _gameRealtimePriorityStateLabel.ForeColor = Color.Orange;
        }
    }

    private void LoadNvidiaOverlayPreLaunchSettings()
    {
        var settings = _appSettingsService.Load();
        _nvidiaOverlayLaunchDotaCheckBox.Checked = settings.NvidiaOverlayPreLaunchDota2;
        _nvidiaOverlayLaunchScpSlCheckBox.Checked = settings.NvidiaOverlayPreLaunchScpSl;
        _nvidiaOverlayLaunchCustomCheckBox.Checked = settings.NvidiaOverlayPreLaunchCustomProgram;
        _nvidiaOverlayCustomProgramTextBox.Text = settings.NvidiaOverlayPreLaunchCustomProgramPath ?? string.Empty;
        UpdateNvidiaOverlayPreLaunchState();
    }

    private void SaveNvidiaOverlayPreLaunchSettings()
    {
        if (_nvidiaOverlayLaunchDotaCheckBox == null || _nvidiaOverlayLaunchScpSlCheckBox == null || _nvidiaOverlayLaunchCustomCheckBox == null || _nvidiaOverlayCustomProgramTextBox == null)
            return;

        var settings = _appSettingsService.Load();
        settings.NvidiaOverlayPreLaunchDota2 = _nvidiaOverlayLaunchDotaCheckBox.Checked;
        settings.NvidiaOverlayPreLaunchScpSl = _nvidiaOverlayLaunchScpSlCheckBox.Checked;
        settings.NvidiaOverlayPreLaunchCustomProgram = _nvidiaOverlayLaunchCustomCheckBox.Checked;
        settings.NvidiaOverlayPreLaunchCustomProgramPath = _nvidiaOverlayCustomProgramTextBox.Text.Trim();
        _appSettingsService.Save(settings);
        UpdateNvidiaOverlayPreLaunchState();
    }

    private void UpdateNvidiaOverlayPreLaunchState()
    {
        var names = GetSelectedNvidiaOverlayPreLaunchNames();
        if (names.Count == 0)
        {
            _nvidiaOverlayPreLaunchStateLabel.Text = "Выберите, для каких .exe включить автоматический перезапуск NVIDIA Overlay из NVIDIA App.";
            _nvidiaOverlayPreLaunchStateLabel.ForeColor = Color.Gray;
            return;
        }

        _nvidiaOverlayPreLaunchStateLabel.Text = "NVIDIA Overlay будет проверяться для: " + string.Join(", ", names);
        _nvidiaOverlayPreLaunchStateLabel.ForeColor = Color.Gainsboro;
    }

    private List<string> GetSelectedNvidiaOverlayPreLaunchNames()
    {
        var names = new List<string>();

        if (_nvidiaOverlayLaunchDotaCheckBox.Checked)
            names.Add("Dota 2");

        if (_nvidiaOverlayLaunchScpSlCheckBox.Checked)
            names.Add("SCP:SL");

        if (_nvidiaOverlayLaunchCustomCheckBox.Checked)
            names.Add("Своя программа");

        return names;
    }

    private void SelectNvidiaOverlayCustomProgram()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите программу",
            Filter = "Программы (*.exe)|*.exe|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _nvidiaOverlayCustomProgramTextBox.Text = dialog.FileName;
        _nvidiaOverlayLaunchCustomCheckBox.Checked = true;
        SaveNvidiaOverlayPreLaunchSettings();
    }

    private void RegisterUnsafeCheckBox(CheckBox checkBox)
    {
        checkBox.CheckedChanged += UnsafeCheckBox_CheckedChanged;
    }

    private void UnsafeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizingUnsafeControls || sender is not CheckBox checkBox || !checkBox.Checked)
            return;

        if (EnsureUnsafeTweaksAllowed("выбор \"" + GetCleanUnsafeText(checkBox.Text) + "\""))
            return;

        _isSynchronizingUnsafeControls = true;
        checkBox.Checked = false;
        _isSynchronizingUnsafeControls = false;
    }

    private static string GetCleanUnsafeText(string text)
    {
        return text.Replace(UnsafeTweaksPrompt.Marker, string.Empty, StringComparison.Ordinal).Trim();
    }

    private static readonly Font GameTweakDescriptionFont = new("Segoe UI", 9.5F);

    private void PopulateGameTweaksPanel()
    {
        // Пересборка сохраняет отмеченные пользователем, но ещё не применённые галочки.
        var previousStates = _gameTweakCheckBoxes.ToDictionary(pair => pair.Key, pair => pair.Value.Checked);
        var hadPreviousStates = _gameTweakCheckBoxes.Count > 0;

        _gameTweakCheckBoxes.Clear();
        UiTheme.ClearAndDisposeControls(_gameTweaksPanel);

        var y = 0;
        var rowWidth = Math.Max(520, _gameTweaksPanel.ClientSize.Width - 8);
        var descriptionWidth = Math.Max(260, rowWidth - 44);
        var descriptionFont = GameTweakDescriptionFont;
        foreach (var tweak in GameTweaks)
        {
            var requiresAdmin = tweak.Values.Any(value => value.Root == RegistryRoot.LocalMachine);
            var descriptionText = requiresAdmin
                ? tweak.Description + " Требует прав администратора."
                : tweak.Description + " Не требует прав администратора.";
            var descriptionSize = TextRenderer.MeasureText(
                descriptionText,
                descriptionFont,
                new Size(descriptionWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
            var descriptionHeight = Math.Max(24, descriptionSize.Height + 6);
            var rowHeight = 38 + descriptionHeight;

            var rowPanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(rowWidth, rowHeight),
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None
            };

            var badge = UiTheme.CreateImpactBadge(tweak.Impact);

            var checkBox = new CheckBox
            {
                Text = tweak.Name,
                Location = new Point(0, 4),
                Size = new Size(Math.Max(200, rowWidth - badge.Width - 24), 25),
                AutoSize = false,
                UseMnemonic = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Checked = hadPreviousStates && previousStates.TryGetValue(tweak, out var wasChecked) && wasChecked
            };

            var descriptionLabel = new Label
            {
                Text = descriptionText,
                Location = new Point(26, 30),
                Size = new Size(descriptionWidth, descriptionHeight),
                AutoSize = false,
                UseMnemonic = false,
                Font = descriptionFont,
                ForeColor = Color.Gainsboro,
                BackColor = Color.Transparent
            };

            badge.Location = new Point(rowWidth - badge.Width - 12, 6);

            _gameTweakCheckBoxes[tweak] = checkBox;
            rowPanel.Controls.Add(checkBox);
            rowPanel.Controls.Add(descriptionLabel);
            rowPanel.Controls.Add(badge);
            _gameTweaksPanel.Controls.Add(rowPanel);
            y += rowHeight + 6;
        }

        _gameTweaksPanel.Height = y + 12;
        _gameTweaksPanel.AutoScrollMinSize = new Size(0, y + 12);
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
            if (!value.IsEnabled(key?.GetValue(value.Name)))
                return false;
        }

        return true;
    }

    private void SetRecommendedGameTweaksChecked()
    {
        SetGameTweaksChecked(tweak => tweak.Recommended);

        ShowStatus("Выбран игровой профиль", Color.Green);
    }

    private void SetSafeGameTweaksChecked()
    {
        SetGameTweaksChecked(tweak => tweak.Recommended && tweak.Values.All(value => value.Root == RegistryRoot.CurrentUser));
        ShowStatus("Выбран безопасный профиль без HKLM-твиков", Color.Green);
    }

    private void SetExperimentalGameTweaksChecked()
    {
        SetGameTweaksChecked(_ => true);
        ShowStatus("Выбран экспериментальный профиль", Color.Orange);
    }

    private void SetGameTweaksChecked(Func<RegistryGameTweak, bool> selector)
    {
        foreach (var (tweak, checkBox) in _gameTweakCheckBoxes)
            checkBox.Checked = selector(tweak);
    }

    private bool ConfirmGameTweaksPreview(IReadOnlyList<PlannedGameTweakChange> plannedChanges)
    {
        var preview = new StringBuilder();
        preview.AppendLine("Будут применены следующие изменения:");
        preview.AppendLine();

        foreach (var change in plannedChanges.Take(12))
        {
            preview.Append(change.Enable ? "Включить: " : "Отключить: ");
            preview.AppendLine(change.Tweak.Name);
        }

        if (plannedChanges.Count > 12)
        {
            preview.AppendLine();
            preview.AppendLine("И ещё: " + (plannedChanges.Count - 12));
        }

        preview.AppendLine();
        preview.AppendLine("Продолжить?");

        return MessageBox.Show(
            preview.ToString(),
            "Перед применением",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information) == DialogResult.Yes;
    }

    private async Task ApplyGameTweaksAsync()
    {
        var plannedChanges = _gameTweakCheckBoxes
            .Where(pair => pair.Value.Checked != IsGameTweakEnabled(pair.Key))
            .Select(pair => new PlannedGameTweakChange(pair.Key, pair.Value.Checked))
            .ToList();

        if (plannedChanges.Count == 0)
        {
            ShowStatus("Нет изменений для применения", Color.Gray);
            return;
        }

        if (!ConfirmGameTweaksPreview(plannedChanges))
            return;

        var hasMachineTweaks = plannedChanges.Any(change =>
            change.Tweak.Values.Any(value => value.Root == RegistryRoot.LocalMachine));

        if (hasMachineTweaks && !IsRunningAsAdministrator())
        {
            ShowStatus("Для HKLM-твиков нужен запуск от имени администратора", Color.Orange);
            return;
        }

        if (hasMachineTweaks && MessageBox.Show(
            "Часть выбранных игровых твиков меняет HKLM и требует запуск от имени администратора. Продолжить применение?",
            "Игровые твики",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        // Применяются только изменённые твики: раньше цикл шёл по всем чекбоксам и
        // каждое нажатие «Применить» молча перезаписывало ~50 значений реестра,
        // включая выключение HAGS и возврат Game DVR у нетронутых пунктов.
        var (applied, failed) = await Task.Run(() =>
        {
            var appliedCount = 0;
            var failedCount = 0;

            foreach (var change in plannedChanges)
            {
                try
                {
                    ApplyGameTweak(change.Tweak, change.Enable);
                    appliedCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            return (appliedCount, failedCount);
        });

        LoadGameTweaksState();
        ShowStatus(failed == 0 ? $"Игровые твики применены: {applied}" : $"Применено: {applied}, ошибок: {failed}", failed == 0 ? Color.Green : Color.Orange);
    }

    private void ApplyGameTweak(RegistryGameTweak tweak, bool enabled)
    {
        foreach (var value in tweak.Values)
        {
            CaptureRegistryBackup("Windows game tweaks", value.Root, value.KeyPath, value.Name);

            using var key = CreateRegistryKey(value.Root, value.KeyPath);
            if (key == null)
                continue;

            if (enabled)
            {
                key.SetValue(value.Name, value.EnabledValue, value.ValueKind);
                continue;
            }

            if (value.DisabledValue != null)
                key.SetValue(value.Name, value.DisabledValue, value.ValueKind);
            else if (key.GetValue(value.Name) != null)
                key.DeleteValue(value.Name, false);
        }
    }

    private async Task RestoreRegistryBackupAsync()
    {
        if (_registryBackupService == null)
        {
            ShowStatus("Сервис бэкапа реестра недоступен", Color.Orange);
            return;
        }

        var result = MessageBox.Show(
            "Будут восстановлены значения реестра, которые ArbuzTweaker сохранил перед применением твиков. Для HKLM может потребоваться запуск от имени администратора.\n\nПродолжить?",
            "Откат бэкапа реестра",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        var restoreResult = await Task.Run(() => _registryBackupService.RestoreAll());
        LoadStateAsync();
        ShowStatus(
            restoreResult.Failed == 0
                ? $"Бэкап реестра восстановлен: {restoreResult.Restored}"
                : $"Восстановлено: {restoreResult.Restored}, ошибок: {restoreResult.Failed}",
            restoreResult.Failed == 0 ? Color.Green : Color.Orange);
    }

    private void CaptureRegistryBackup(string group, RegistryRoot root, string keyPath, string valueName)
    {
        _registryBackupService?.CaptureValue(group, GetRegistryRootName(root), keyPath, valueName);
    }

    private static string GetRegistryRootName(RegistryRoot root)
    {
        return root == RegistryRoot.CurrentUser ? "HKCU" : "HKLM";
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
        if (IsSafeModeBlocked("твик Ndu"))
            return;

        if (!IsRunningAsAdministrator())
        {
            ShowStatus("Для Ndu нужен запуск от имени администратора", Color.Orange);
            return;
        }

        var targetValue = _nduCheckBox.Checked ? 4 : 2;

        try
        {
            CaptureRegistryBackup("Windows system tweaks", RegistryRoot.LocalMachine, NduRegistryPath, "Start");
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
            await LoadIpv6StateAsync();
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
        if (IsSafeModeBlocked("DisableDHCPMediaSense"))
            return;

        if (!IsRunningAsAdministrator())
        {
            ShowStatus("Для DisableDHCPMediaSense нужен запуск от имени администратора", Color.Orange);
            return;
        }

        if (!ConfirmNetworkOperation("Будет изменён параметр DisableDHCPMediaSense в TCP/IP."))
            return;

        var targetValue = _dhcpMediaSenseCheckBox.Checked ? 1 : 0;

        try
        {
            CaptureRegistryBackup("Windows network tweaks", RegistryRoot.LocalMachine, TcpipParametersRegistryPath, "DisableDHCPMediaSense");
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
            await LoadIpv6StateAsync();
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

    private async Task ApplyMpoSettingAsync()
    {
        CaptureRegistryBackup("Windows graphics tweaks", RegistryRoot.LocalMachine, DwmRegistryPath, "OverlayTestMode");

        var script = _mpoDisabledCheckBox.Checked
            ? "$key = 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm'; New-Item -Path $key -Force | Out-Null; New-ItemProperty -Path $key -Name OverlayTestMode -PropertyType DWord -Value 5 -Force | Out-Null"
            : "$key = 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm'; if (Test-Path $key) { Remove-ItemProperty -Path $key -Name OverlayTestMode -ErrorAction SilentlyContinue }";

        var commandResult = await RunElevatedPowerShellAsync(script);
        if (commandResult == ElevatedCommandResult.Success)
        {
            LoadMpoState();
            ShowStatus(_mpoDisabledCheckBox.Checked ? "MPO отключено" : "MPO включено", Color.Green);
            MessageBox.Show(
                "Изменение MPO применено. Для полного эффекта NVIDIA рекомендует перезагрузить Windows.",
                "Multi-Plane Overlay",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            LoadMpoState();
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        LoadMpoState();
        ShowStatus("Не удалось изменить состояние MPO", Color.Orange);
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
            await LoadIpv6StateAsync();
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
        if (!IsRunningAsAdministrator())
        {
            ShowStatus("Для политики Edge нужен запуск от имени администратора", Color.Orange);
            return;
        }

        var targetValue = _edgeStartupBoostCheckBox.Checked ? 0 : 1;

        try
        {
            CaptureRegistryBackup("Windows system tweaks", RegistryRoot.LocalMachine, EdgePolicyRegistryPath, "StartupBoostEnabled");
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

    private async Task ApplyNvidiaOverlayRestartAsync()
    {
        if (_nvidiaOverlayRestartCheckBox.Checked && !IsNvidiaOverlayAvailable())
        {
            var dialogResult = MessageBox.Show(
                "NVIDIA App не найдена по стандартным путям. Создать задачу планировщика всё равно?",
                "NVIDIA Overlay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (dialogResult != DialogResult.Yes)
            {
                await LoadNvidiaOverlayRestartStateAsync();
                return;
            }
        }

        ShowStatus(_nvidiaOverlayRestartCheckBox.Checked ? "Создаётся задача NVIDIA Overlay..." : "Удаляется задача NVIDIA Overlay...", Color.Gray);

        var script = _nvidiaOverlayRestartCheckBox.Checked
            ? BuildNvidiaOverlayRegisterTaskScript()
            : BuildNvidiaOverlayRemoveTaskScript();
        var commandResult = await RunElevatedPowerShellAsync(script);

        if (commandResult == ElevatedCommandResult.Success)
        {
            await LoadNvidiaOverlayRestartStateAsync();
            ShowStatus(_nvidiaOverlayRestartCheckBox.Checked ? "Задача NVIDIA Overlay включена" : "Задача NVIDIA Overlay удалена", Color.Green);
            return;
        }

        if (commandResult == ElevatedCommandResult.Cancelled)
        {
            await LoadNvidiaOverlayRestartStateAsync();
            ShowStatus("Операция отменена", Color.Orange);
            return;
        }

        await LoadNvidiaOverlayRestartStateAsync();
        ShowStatus("Не удалось изменить задачу NVIDIA Overlay", Color.Orange);
    }

    private static bool IsNvidiaOverlayAvailable()
    {
        return File.Exists(NvidiaOverlayExePath) || File.Exists(NvidiaOverlayHelperPath);
    }

    private static string BuildNvidiaOverlayRegisterTaskScript()
    {
        var scriptPath = GetNvidiaOverlayRestartScriptPath();
        var restartScript = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "Start-Sleep -Seconds 3",
            "$overlayPath = " + QuotePowerShellString(NvidiaOverlayExePath),
            "$helperPath = " + QuotePowerShellString(NvidiaOverlayHelperPath),
            "$existingOverlay = Get-Process -Name 'NVIDIA Overlay' -ErrorAction SilentlyContinue",
            "if ($null -ne $existingOverlay) { exit 0 }",
            "if (Test-Path -LiteralPath $overlayPath) {",
            "    Start-Process -FilePath $overlayPath -WindowStyle Hidden",
            "    exit 0",
            "}",
            "if (Test-Path -LiteralPath $helperPath) {",
            "    Start-Process -FilePath $helperPath -WindowStyle Hidden",
            "}"
        });

        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(NvidiaOverlayRestartTaskName),
            "$restartScriptPath = " + QuotePowerShellString(scriptPath),
            "New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($restartScriptPath)) -Force | Out-Null",
            "Set-Content -LiteralPath $restartScriptPath -Encoding UTF8 -Force -Value @'",
            restartScript,
            "'@",
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"try { $taskFolder = $service.GetFolder('\' + $taskFolderName) } catch { $taskFolder = $rootFolder.CreateFolder($taskFolderName) }",
            "$definition = $service.NewTask(0)",
            "$definition.RegistrationInfo.Author = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.RegistrationInfo.Description = 'Проверяет NVIDIA Overlay при входе в Windows и после выхода из сна; запускает только если он не работает.'",
            "$definition.Principal.UserId = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.Principal.LogonType = 3",
            "$definition.Principal.RunLevel = 0",
            "$definition.Settings.Enabled = $true",
            "$definition.Settings.Hidden = $true",
            "$definition.Settings.AllowDemandStart = $true",
            "$definition.Settings.StartWhenAvailable = $true",
            "$definition.Settings.DisallowStartIfOnBatteries = $false",
            "$definition.Settings.StopIfGoingOnBatteries = $false",
            "$definition.Settings.MultipleInstances = 2",
            "$definition.Settings.ExecutionTimeLimit = 'PT2M'",
            "$logonTrigger = $definition.Triggers.Create(9)",
            "$logonTrigger.Enabled = $true",
            "$logonTrigger.Delay = 'PT10S'",
            "$eventTrigger = $definition.Triggers.Create(0)",
            "$eventTrigger.Enabled = $true",
            "$eventTrigger.Delay = 'PT10S'",
            "$eventTrigger.Subscription = '<QueryList><Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*[System[Provider[@Name=''Microsoft-Windows-Power-Troubleshooter''] and EventID=1]]</Select></Query></QueryList>'",
            "$action = $definition.Actions.Create(0)",
            "$action.Path = 'powershell.exe'",
            "$action.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"' + $restartScriptPath + '\"'",
            "$action.WorkingDirectory = [System.IO.Path]::GetDirectoryName($restartScriptPath)",
            "$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$taskFolder.RegisterTaskDefinition($taskName, $definition, 6, $currentUser, $null, 3) | Out-Null"
        });
    }

    private static string BuildNvidiaOverlayRemoveTaskScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(NvidiaOverlayRestartTaskName),
            "$restartScriptPath = " + QuotePowerShellString(GetNvidiaOverlayRestartScriptPath()),
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"$taskFolder = $service.GetFolder('\' + $taskFolderName)",
            "$taskFolder.DeleteTask($taskName, 0)",
            "Remove-Item -LiteralPath $restartScriptPath -Force",
            "$rootFolder.DeleteFolder($taskFolderName, 0)",
            "exit 0"
        });
    }

    private static string GetNvidiaOverlayRestartScriptPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArbuzTweaker", "RestartNvidiaOverlay.ps1");
    }

    private static string QuotePowerShellString(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private async Task ApplyNvidiaOverlayPreLaunchAsync()
    {
        SaveNvidiaOverlayPreLaunchSettings();

        var selectedNames = GetSelectedNvidiaOverlayPreLaunchNames();
        if (selectedNames.Count == 0)
        {
            ShowStatus("Удаляется автозапуск Overlay для программ...", Color.Gray);
            var removeResult = await RunElevatedPowerShellAsync(BuildNvidiaOverlayProcessRemoveTaskScript());
            if (removeResult == ElevatedCommandResult.Success)
            {
                ShowStatus("Автозапуск Overlay для программ отключён", Color.Green);
                return;
            }

            ShowStatus(removeResult == ElevatedCommandResult.Cancelled ? "Операция отменена" : "Не удалось отключить автозапуск Overlay", Color.Orange);
            return;
        }

        var resolveResult = await ResolveNvidiaOverlayProcessTargetsAsync();
        if (resolveResult.Errors.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, resolveResult.Errors),
                "NVIDIA Overlay",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            ShowStatus("Не удалось подготовить автозапуск Overlay", Color.Orange);
            return;
        }

        if (!IsNvidiaOverlayAvailable())
        {
            var dialogResult = MessageBox.Show(
                "NVIDIA App не найдена по стандартным путям. Создать задачу автозапуска всё равно?",
                "NVIDIA Overlay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (dialogResult != DialogResult.Yes)
                return;
        }

        ShowStatus("Создаётся автозапуск Overlay для выбранных программ...", Color.Gray);
        var commandResult = await RunElevatedPowerShellAsync(BuildNvidiaOverlayProcessRegisterTaskScript(resolveResult.Targets));

        if (commandResult == ElevatedCommandResult.Success)
        {
            UpdateNvidiaOverlayPreLaunchState();
            ShowStatus("Автозапуск Overlay для выбранных программ включён", Color.Green);
            return;
        }

        ShowStatus(commandResult == ElevatedCommandResult.Cancelled ? "Операция отменена" : "Не удалось включить автозапуск Overlay", Color.Orange);
    }

    private async Task ApplyGameRealtimePriorityAsync()
    {
        if (_gameRealtimePriorityCheckBox.Checked)
        {
            if (IsSafeModeBlocked("автоматическое назначение приоритета реального времени играм"))
            {
                await LoadGameRealtimePriorityStateAsync();
                return;
            }

            var confirmation = MessageBox.Show(
                "Dota 2 и SCP:SL будут получать приоритет реального времени при запуске. Такой приоритет может отнять ресурсы у Windows, драйверов, звука и ввода, из-за чего система может зависнуть или стать неотзывчивой.\n\nПродолжить?",
                "Приоритет реального времени",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
            {
                await LoadGameRealtimePriorityStateAsync();
                return;
            }

            var targets = await ResolveGameRealtimePriorityTargetsAsync();
            if (targets.Count == 0)
            {
                MessageBox.Show(
                    "Не удалось найти Dota 2 или SCP:SL. Установите хотя бы одну из игр через Steam и повторите попытку.",
                    "Приоритет реального времени",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                await LoadGameRealtimePriorityStateAsync();
                return;
            }

            ShowStatus("Создаётся задача приоритета реального времени...", Color.Gray);
            var registerResult = await RunElevatedPowerShellAsync(BuildGameRealtimePriorityRegisterTaskScript(targets));
            await LoadGameRealtimePriorityStateAsync();
            ShowStatus(
                registerResult == ElevatedCommandResult.Success
                    ? "Автоматический приоритет реального времени включён"
                    : registerResult == ElevatedCommandResult.Cancelled
                        ? "Операция отменена"
                        : "Не удалось создать задачу приоритета игр",
                registerResult == ElevatedCommandResult.Success ? Color.Green : Color.Orange);
            return;
        }

        ShowStatus("Удаляется задача приоритета игр...", Color.Gray);
        var removeResult = await RunElevatedPowerShellAsync(BuildGameRealtimePriorityRemoveTaskScript());
        await LoadGameRealtimePriorityStateAsync();
        ShowStatus(
            removeResult == ElevatedCommandResult.Success
                ? "Автоматический приоритет реального времени отключён"
                : removeResult == ElevatedCommandResult.Cancelled
                    ? "Операция отменена"
                    : "Не удалось удалить задачу приоритета игр",
            removeResult == ElevatedCommandResult.Success ? Color.Green : Color.Orange);
    }

    private async Task<IReadOnlyList<GameProcessTarget>> ResolveGameRealtimePriorityTargetsAsync()
    {
        var targets = new List<GameProcessTarget>();

        var dotaExePath = await GetDota2ExecutablePathAsync();
        if (!string.IsNullOrWhiteSpace(dotaExePath))
            targets.Add(new GameProcessTarget("Dota 2", dotaExePath));

        var scpSlExePath = await GetScpSlExecutablePathAsync();
        if (!string.IsNullOrWhiteSpace(scpSlExePath))
            targets.Add(new GameProcessTarget("SCP:SL", scpSlExePath));

        return targets
            .GroupBy(target => target.ProcessPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<NvidiaOverlayProcessTargetsResolveResult> ResolveNvidiaOverlayProcessTargetsAsync()
    {
        var targets = new List<NvidiaOverlayProcessTarget>();
        var errors = new List<string>();

        if (_nvidiaOverlayLaunchDotaCheckBox.Checked)
        {
            var dotaExePath = await GetDota2ExecutablePathAsync();
            if (string.IsNullOrWhiteSpace(dotaExePath))
                errors.Add("Dota 2 не найдена. Откройте Steam/установите игру или укажите свою программу вручную.");
            else
                targets.Add(new NvidiaOverlayProcessTarget("Dota 2", dotaExePath));
        }

        if (_nvidiaOverlayLaunchScpSlCheckBox.Checked)
        {
            var scpSlExePath = await GetScpSlExecutablePathAsync();
            if (string.IsNullOrWhiteSpace(scpSlExePath))
                errors.Add("SCP:SL не найдена. Откройте Steam/установите игру или укажите свою программу вручную.");
            else
                targets.Add(new NvidiaOverlayProcessTarget("SCP:SL", scpSlExePath));
        }

        if (_nvidiaOverlayLaunchCustomCheckBox.Checked)
        {
            var customProgramPath = _nvidiaOverlayCustomProgramTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(customProgramPath))
                errors.Add("Не задан путь к своей программе.");
            else if (!File.Exists(customProgramPath))
                errors.Add("Своя программа не найдена: " + customProgramPath);
            else
                targets.Add(new NvidiaOverlayProcessTarget("Своя программа", customProgramPath));
        }

        var distinctTargets = targets
            .GroupBy(target => target.ProcessPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return new NvidiaOverlayProcessTargetsResolveResult(distinctTargets, errors);
    }

    private async Task<string?> GetDota2ExecutablePathAsync()
    {
        var (dotaPath, _) = await _dota2Service.FindDota2Async();
        if (string.IsNullOrWhiteSpace(dotaPath))
            return null;

        var candidates = new[]
        {
            Path.Combine(dotaPath, "game", "bin", "win64", "dota2.exe"),
            Path.Combine(dotaPath, "game", "bin", "win32", "dota2.exe"),
            Path.Combine(dotaPath, "dota2.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<string?> GetScpSlExecutablePathAsync()
    {
        var (gamePath, _) = await _scpSlService.FindGameAsync();
        if (string.IsNullOrWhiteSpace(gamePath))
            return null;

        var candidates = new[]
        {
            Path.Combine(gamePath, "SCPSL.exe"),
            Path.Combine(gamePath, "SCP Secret Laboratory.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string BuildNvidiaOverlayProcessRegisterTaskScript(IReadOnlyList<NvidiaOverlayProcessTarget> targets)
    {
        var scriptPath = GetNvidiaOverlayProcessRestartScriptPath();
        var restartScript = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$stampPath = Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\\NvidiaOverlayProcessRestart.stamp'",
            "New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($stampPath)) -Force | Out-Null",
            "if (Test-Path -LiteralPath $stampPath) { if (((Get-Date) - (Get-Item -LiteralPath $stampPath).LastWriteTime).TotalSeconds -lt 15) { exit 0 } }",
            "Set-Content -LiteralPath $stampPath -Value ([DateTime]::Now.ToString('O')) -Force",
            "$overlayPath = " + QuotePowerShellString(NvidiaOverlayExePath),
            "$helperPath = " + QuotePowerShellString(NvidiaOverlayHelperPath),
            "$existingOverlay = Get-Process -Name 'NVIDIA Overlay' -ErrorAction SilentlyContinue",
            "if ($null -ne $existingOverlay) { exit 0 }",
            "if (Test-Path -LiteralPath $overlayPath) {",
            "    Start-Process -FilePath $overlayPath -WindowStyle Hidden",
            "    exit 0",
            "}",
            "if (Test-Path -LiteralPath $helperPath) {",
            "    Start-Process -FilePath $helperPath -WindowStyle Hidden",
            "}"
        });
        var subscription = BuildProcessCreationEventSubscription(targets.Select(target => target.ProcessPath));

        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(NvidiaOverlayProcessRestartTaskName),
            "$restartScriptPath = " + QuotePowerShellString(scriptPath),
            "New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($restartScriptPath)) -Force | Out-Null",
            "Set-Content -LiteralPath $restartScriptPath -Encoding UTF8 -Force -Value @'",
            restartScript,
            "'@",
            "$auditPolPath = Join-Path $env:SystemRoot 'System32\\auditpol.exe'",
            "& $auditPolPath /set '/subcategory:{0CCE922B-69AE-11D9-BED3-505054503030}' /success:enable | Out-Null",
            "if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }",
            BuildProcessAuditStampSnippet(),
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"try { $taskFolder = $service.GetFolder('\' + $taskFolderName) } catch { $taskFolder = $rootFolder.CreateFolder($taskFolderName) }",
            "$definition = $service.NewTask(0)",
            "$definition.RegistrationInfo.Author = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.RegistrationInfo.Description = 'Проверяет NVIDIA Overlay при запуске выбранных игр или программ; запускает только если он не работает.'",
            "$definition.Principal.UserId = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.Principal.LogonType = 3",
            "$definition.Principal.RunLevel = 0",
            "$definition.Settings.Enabled = $true",
            "$definition.Settings.Hidden = $true",
            "$definition.Settings.AllowDemandStart = $true",
            "$definition.Settings.StartWhenAvailable = $true",
            "$definition.Settings.DisallowStartIfOnBatteries = $false",
            "$definition.Settings.StopIfGoingOnBatteries = $false",
            "$definition.Settings.MultipleInstances = 2",
            "$definition.Settings.ExecutionTimeLimit = 'PT2M'",
            "$eventTrigger = $definition.Triggers.Create(0)",
            "$eventTrigger.Enabled = $true",
            "$eventTrigger.Subscription = " + QuotePowerShellString(subscription),
            "$action = $definition.Actions.Create(0)",
            "$action.Path = 'powershell.exe'",
            "$action.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"' + $restartScriptPath + '\"'",
            "$action.WorkingDirectory = [System.IO.Path]::GetDirectoryName($restartScriptPath)",
            "$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$taskFolder.RegisterTaskDefinition($taskName, $definition, 6, $currentUser, $null, 3) | Out-Null"
        });
    }

    private static string BuildNvidiaOverlayProcessRemoveTaskScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(NvidiaOverlayProcessRestartTaskName),
            "$otherAuditTaskName = " + QuotePowerShellString(GameRealtimePriorityTaskName),
            "$restartScriptPath = " + QuotePowerShellString(GetNvidiaOverlayProcessRestartScriptPath()),
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"$taskFolder = $service.GetFolder('\' + $taskFolderName)",
            "$taskFolder.DeleteTask($taskName, 0)",
            "Remove-Item -LiteralPath $restartScriptPath -Force",
            BuildDisableProcessAuditIfUnusedSnippet(),
            "$rootFolder.DeleteFolder($taskFolderName, 0)",
            "exit 0"
        });
    }

    private static string BuildGameRealtimePriorityRegisterTaskScript(IReadOnlyList<GameProcessTarget> targets)
    {
        var scriptPath = GetGameRealtimePriorityScriptPath();
        var targetPaths = string.Join(", ", targets.Select(target => QuotePowerShellString(target.ProcessPath)));
        var priorityScript = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$targetPaths = @( " + targetPaths + " )",
            "for ($attempt = 0; $attempt -lt 10; $attempt++) {",
            "    $matchedProcess = $false",
            "    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.ExecutablePath -and $targetPaths -contains $_.ExecutablePath } | ForEach-Object {",
            "        try {",
            "            $process = Get-Process -Id ([int]$_.ProcessId) -ErrorAction Stop",
            "            $process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::RealTime",
            "            $matchedProcess = $true",
            "        } catch { }",
            "    }",
            "    if ($matchedProcess) { exit 0 }",
            "    Start-Sleep -Milliseconds 500",
            "}",
            "exit 1"
        });
        var subscription = BuildProcessCreationEventSubscription(targets.Select(target => target.ProcessPath));

        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(GameRealtimePriorityTaskName),
            "$priorityScriptPath = " + QuotePowerShellString(scriptPath),
            "New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($priorityScriptPath)) -Force | Out-Null",
            "Set-Content -LiteralPath $priorityScriptPath -Encoding UTF8 -Force -Value @'",
            priorityScript,
            "'@",
            "$auditPolPath = Join-Path $env:SystemRoot 'System32\\auditpol.exe'",
            "& $auditPolPath /set '/subcategory:{0CCE922B-69AE-11D9-BED3-505054503030}' /success:enable | Out-Null",
            "if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }",
            BuildProcessAuditStampSnippet(),
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"try { $taskFolder = $service.GetFolder('\' + $taskFolderName) } catch { $taskFolder = $rootFolder.CreateFolder($taskFolderName) }",
            "$definition = $service.NewTask(0)",
            "$definition.RegistrationInfo.Author = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.RegistrationInfo.Description = 'Назначает приоритет реального времени Dota 2 и SCP:SL при их запуске.'",
            "$definition.Principal.UserId = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.Principal.LogonType = 3",
            "$definition.Principal.RunLevel = 1",
            "$definition.Settings.Enabled = $true",
            "$definition.Settings.Hidden = $true",
            "$definition.Settings.AllowDemandStart = $true",
            "$definition.Settings.StartWhenAvailable = $true",
            "$definition.Settings.DisallowStartIfOnBatteries = $false",
            "$definition.Settings.StopIfGoingOnBatteries = $false",
            "$definition.Settings.MultipleInstances = 2",
            "$definition.Settings.ExecutionTimeLimit = 'PT2M'",
            "$eventTrigger = $definition.Triggers.Create(0)",
            "$eventTrigger.Enabled = $true",
            "$eventTrigger.Subscription = " + QuotePowerShellString(subscription),
            "$action = $definition.Actions.Create(0)",
            "$action.Path = 'powershell.exe'",
            "$action.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"' + $priorityScriptPath + '\"'",
            "$action.WorkingDirectory = [System.IO.Path]::GetDirectoryName($priorityScriptPath)",
            "$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$taskFolder.RegisterTaskDefinition($taskName, $definition, 6, $currentUser, $null, 3) | Out-Null"
        });
    }

    private static string BuildGameRealtimePriorityRemoveTaskScript()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$taskFolderName = " + QuotePowerShellString(NvidiaOverlayTaskFolderName),
            "$taskName = " + QuotePowerShellString(GameRealtimePriorityTaskName),
            "$otherAuditTaskName = " + QuotePowerShellString(NvidiaOverlayProcessRestartTaskName),
            "$priorityScriptPath = " + QuotePowerShellString(GetGameRealtimePriorityScriptPath()),
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"try { $taskFolder = $service.GetFolder('\' + $taskFolderName); $taskFolder.DeleteTask($taskName, 0) } catch { }",
            "Remove-Item -LiteralPath $priorityScriptPath -Force",
            BuildDisableProcessAuditIfUnusedSnippet(),
            "exit 0"
        });
    }

    // Аудит создания процессов (событие 4688) — глобальная политика. Выключаем его,
    // только если его включал сам твикер и ни одна из двух задач на 4688 больше не существует.
    private static string BuildDisableProcessAuditIfUnusedSnippet()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$auditStampPath = Join-Path $env:LOCALAPPDATA 'ArbuzTweaker\\ProcessAuditEnabledByArbuz.stamp'",
            "$otherTaskExists = $false",
            @"try { $checkFolder = $service.GetFolder('\' + $taskFolderName); $null = $checkFolder.GetTask($otherAuditTaskName); $otherTaskExists = $true } catch { }",
            "if (-not $otherTaskExists -and (Test-Path -LiteralPath $auditStampPath)) {",
            "    $auditPolPath = Join-Path $env:SystemRoot 'System32\\auditpol.exe'",
            "    & $auditPolPath /set '/subcategory:{0CCE922B-69AE-11D9-BED3-505054503030}' /success:disable | Out-Null",
            "    Remove-Item -LiteralPath $auditStampPath -Force",
            "}"
        });
    }

    private static string BuildProcessAuditStampSnippet()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$auditStampDir = Join-Path $env:LOCALAPPDATA 'ArbuzTweaker'",
            "New-Item -ItemType Directory -Path $auditStampDir -Force | Out-Null",
            "Set-Content -LiteralPath (Join-Path $auditStampDir 'ProcessAuditEnabledByArbuz.stamp') -Value ([DateTime]::Now.ToString('O')) -Force"
        });
    }

    private static string BuildProcessCreationEventSubscription(IEnumerable<string> processPaths)
    {
        // Сравнение строк в XPath фильтра событий регистрозависимо, а путь из реестра Steam
        // приходит в нижнем регистре — событие 4688 пишет NewProcessName в реальном регистре
        // диска, и фильтр никогда не срабатывал. Регистр восстанавливается по файловой системе.
        var conditions = string.Join(" or ", processPaths.Select(path => "Data[@Name='NewProcessName']=" + QuoteEventXPathString(GetExactPathCasing(path))));
        return "<QueryList><Query Id=\"0\" Path=\"Security\"><Select Path=\"Security\">*[System[Provider[@Name='Microsoft-Windows-Security-Auditing'] and EventID=4688]] and *[EventData[" + conditions + "]]</Select></Query></QueryList>";
    }

    private static string GetExactPathCasing(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
                return path;

            var current = root.ToUpperInvariant();
            foreach (var segment in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                var matches = Directory.GetFileSystemEntries(current, segment);
                current = matches.Length == 1 ? matches[0] : Path.Combine(current, segment);
            }

            return current;
        }
        catch
        {
            return path;
        }
    }

    private static string QuoteEventXPathString(string value)
    {
        var escaped = value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        return !escaped.Contains('"')
            ? "\"" + escaped + "\""
            : "'" + escaped.Replace("'", "&apos;") + "'";
    }

    private static string GetNvidiaOverlayProcessRestartScriptPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArbuzTweaker", "RestartNvidiaOverlayOnProcessStart.ps1");
    }

    private static string GetGameRealtimePriorityScriptPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArbuzTweaker", "SetGameRealtimePriority.ps1");
    }

    private static bool ConfirmNetworkOperation(string details)
    {
        return MessageBox.Show(
            details + "\n\nЭто может временно разорвать текущее соединение. Продолжить?",
            "Сетевой твик",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private bool IsSafeModeBlocked(string operationName)
    {
        return !EnsureUnsafeTweaksAllowed("операция \"" + operationName + "\"");
    }

    private bool EnsureUnsafeTweaksAllowed(string actionDescription)
    {
        var settings = _appSettingsService.Load();
        if (!settings.SafeModeUserConfigOnly && settings.UnsafeTweaksRiskAccepted)
            return true;

        if (!UnsafeTweaksPrompt.ConfirmEnable(this, actionDescription))
        {
            ShowStatus("Операция заблокирована безопасным режимом", Color.Orange);
            return false;
        }

        settings.SafeModeUserConfigOnly = false;
        settings.UnsafeTweaksRiskAccepted = true;
        _appSettingsService.Save(settings);
        ShowStatus("Небезопасные твики разрешены", Color.Orange);
        return true;
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static Label CreateAdminStatusLabel()
    {
        var isAdmin = IsRunningAsAdministrator();
        return new Label
        {
            Text = isAdmin
                ? "Статус прав: запущено с правами администратора. Системные HKLM-твики доступны."
                : "Статус прав: запущено без прав администратора. HKLM-твики потребуют подтверждения UAC или не применятся.",
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = isAdmin ? UiTheme.AccentGreen : Color.Orange,
            BackColor = Color.Transparent
        };
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
                if (!process.WaitForExit(ElevatedCommandTimeoutMilliseconds))
                {
                    TryKillProcess(process);
                    return ElevatedCommandResult.Failure;
                }

                return process.ExitCode == 0 ? ElevatedCommandResult.Success : ElevatedCommandResult.Failure;
            }
            catch (Win32Exception exception)
            {
                // 1223 = ERROR_CANCELLED: пользователь отклонил запрос UAC.
                // Остальные Win32-ошибки (файл не найден, доступ запрещён) — это сбой, не отмена.
                return exception.NativeErrorCode == 1223
                    ? ElevatedCommandResult.Cancelled
                    : ElevatedCommandResult.Failure;
            }
            catch
            {
                return ElevatedCommandResult.Failure;
            }
        });
    }

    private static async Task<string> RunPowerShellQueryAsync(string script)
    {
        try
        {
            using var process = new Process();
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!process.Start())
                return string.Empty;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(waitTask, Task.Delay(PowerShellQueryTimeoutMilliseconds));
            if (completedTask != waitTask)
            {
                TryKillProcess(process);
                return string.Empty;
            }

            await waitTask;
            await errorTask;
            return await outputTask;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
        }
    }

    private async void ShowStatus(string message, Color color)
    {
        var messageVersion = ++_statusMessageVersion;
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        _statusLabel.Visible = true;
        await Task.Delay(4000);
        if (IsDisposed || Disposing || messageVersion != _statusMessageVersion)
            return;

        _statusLabel.Text = string.Empty;
        _statusLabel.Visible = false;
    }

    private sealed record RegistryGameTweak(
        string Name,
        string Description,
        bool Recommended,
        IReadOnlyList<RegistryGameValue> Values)
    {
        // Большинство игровых твиков — про FPS/задержку; исключения помечаются явно.
        public UiTheme.Impact Impact { get; init; } = UiTheme.Impact.Fps;
    }

    private sealed record PlannedGameTweakChange(RegistryGameTweak Tweak, bool Enable);

    private sealed record RegistryGameValue(
        RegistryRoot Root,
        string KeyPath,
        string Name,
        object EnabledValue,
        object? DisabledValue,
        RegistryValueKind ValueKind)
    {
        public bool IsEnabled(object? currentValue)
        {
            if (ValueKind == RegistryValueKind.DWord)
                return currentValue is int currentInt
                    && EnabledValue is int enabledInt
                    && currentInt == enabledInt;

            if (currentValue is string currentString && EnabledValue is string enabledString)
                return string.Equals(currentString, enabledString, StringComparison.OrdinalIgnoreCase);

            return Equals(currentValue, EnabledValue);
        }

        public static RegistryGameValue CurrentUser(string keyPath, string name, int enabledValue, int? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.CurrentUser, keyPath, name, enabledValue, disabledValue, RegistryValueKind.DWord);
        }

        public static RegistryGameValue LocalMachine(string keyPath, string name, int enabledValue, int? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.LocalMachine, keyPath, name, enabledValue, disabledValue, RegistryValueKind.DWord);
        }

        public static RegistryGameValue CurrentUserString(string keyPath, string name, string enabledValue, string? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.CurrentUser, keyPath, name, enabledValue, disabledValue, RegistryValueKind.String);
        }

        public static RegistryGameValue LocalMachineString(string keyPath, string name, string enabledValue, string? disabledValue)
        {
            return new RegistryGameValue(RegistryRoot.LocalMachine, keyPath, name, enabledValue, disabledValue, RegistryValueKind.String);
        }
    }

    private sealed record NvidiaOverlayProcessTarget(string Name, string ProcessPath);

    private sealed record GameProcessTarget(string Name, string ProcessPath);

    private sealed record NvidiaOverlayProcessTargetsResolveResult(
        IReadOnlyList<NvidiaOverlayProcessTarget> Targets,
        IReadOnlyList<string> Errors);

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
