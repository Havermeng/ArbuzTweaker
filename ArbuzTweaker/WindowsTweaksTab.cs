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
            }),
        new(
            "[Экспериментально] Низкая задержка Win32-презентации",
            "Пункт из отчёта Platinum: включает Win32LowLatencyPresentationEnabled для текущего пользователя. Может влиять на задержку в оконном и безрамочном режиме.",
            false,
            new[] { RegistryGameValue.CurrentUser(@"Software\Microsoft\Windows\CurrentVersion\GameConfigStore", "Win32LowLatencyPresentationEnabled", 1, null) }),
        new(
            "[Экспериментально] Профиль MMCSS для игр",
            "Пункт из отчёта Platinum: выставляет высокий профиль Tasks\\Games для Multimedia Class Scheduler. Может помочь с приоритетом игр, но эффект зависит от системы.",
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
            "Пункт из отчёта Platinum: разрешает GlobalTimerResolutionRequests в ядре Windows. Иногда снижает микрозадержки, но может увеличить расход батареи.",
            false,
            new[] { RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "GlobalTimerResolutionRequests", 1, 0) }),
        new(
            "[Экспериментально] Распределение таймеров ядра",
            "Пункт из отчёта Platinum: включает DistributeTimers. Это не разгон и не отключение защиты, но проверять стоит только отдельно от других твиков.",
            false,
            new[] { RegistryGameValue.LocalMachine(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel", "DistributeTimers", 1, 0) }),
        new(
            "[Экспериментально] Очередь кадров DWM",
            "Пункт из отчёта Platinum: ставит MaxQueuedBuffers=2 для DWM. Может повлиять на задержку в оконном/безрамочном режиме, эффект зависит от драйвера.",
            false,
            new[] { RegistryGameValue.CurrentUser(@"Software\Microsoft\Windows\DWM", "MaxQueuedBuffers", 2, null) }),
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
            Text = "Перезапускать NVIDIA Overlay при входе в Windows и выходе из сна",
            Location = new Point(20, 1665),
            AutoSize = true,
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            ForeColor = Color.White
        };

        var nvidiaOverlayDescriptionLabel = new Label
        {
            Text = "Создаёт задачу планировщика Windows, которая аккуратно перезапускает только NVIDIA Overlay.exe. Службы NVIDIA Container не трогаются.",
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
            Text = "Запуск с перезапуском NVIDIA Overlay",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 1830),
            AutoSize = true
        };

        var nvidiaOverlayPreLaunchDescriptionLabel = new Label
        {
            Text = "Выберите игры или программу и нажмите Применить. После этого будет автоматически перезапускаться именно внутриигровой NVIDIA Overlay из NVIDIA App при запуске выбранных .exe, без запуска игр из твикера.",
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
            Text = "Применить автоперезапуск",
            Location = new Point(20, 2020),
            Size = new Size(240, 35)
        };
        applyNvidiaOverlayPreLaunchButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyNvidiaOverlayPreLaunchAsync);

        _nvidiaOverlayPreLaunchStateLabel = new Label
        {
            Text = "Выберите, для каких .exe включить автоматический перезапуск NVIDIA Overlay из NVIDIA App.",
            Location = new Point(20, 1992),
            MaximumSize = new Size(WindowsPageContentWidth, 0),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        RegisterUnsafeCheckBox(_nduCheckBox);
        RegisterUnsafeCheckBox(_dhcpMediaSenseCheckBox);
        RegisterUnsafeCheckBox(_googleDnsCheckBox);
        RegisterUnsafeCheckBox(_disableIpv6CheckBox);

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

        var safeProfileButton = new Button
        {
            Text = "Профиль: безопасный",
            Location = new Point(20, 95),
            Size = new Size(190, 35)
        };
        UiTheme.StyleActionButton(safeProfileButton);
        safeProfileButton.Click += (s, e) => SetSafeGameTweaksChecked();

        var gameProfileButton = new Button
        {
            Text = "Профиль: игровой",
            Location = new Point(220, 95),
            Size = new Size(170, 35)
        };
        UiTheme.StyleActionButton(gameProfileButton);
        gameProfileButton.Click += (s, e) => SetRecommendedGameTweaksChecked();

        var experimentalProfileButton = new Button
        {
            Text = "Профиль: экспериментальный",
            Location = new Point(400, 95),
            Size = new Size(240, 35)
        };
        UiTheme.StyleActionButton(experimentalProfileButton);
        experimentalProfileButton.Click += (s, e) => SetExperimentalGameTweaksChecked();

        var applyGameTweaksButton = new Button
        {
            Text = "Применить игровые твики",
            Location = new Point(20, 145),
            Size = new Size(210, 35)
        };
        UiTheme.StyleActionButton(applyGameTweaksButton, true);
        applyGameTweaksButton.Click += async (s, e) => await UiTheme.RunButtonOperationAsync(s, ApplyGameTweaksAsync);

        var restoreRegistryBackupButton = new Button
        {
            Text = "Откатить бэкап реестра",
            Location = new Point(240, 145),
            Size = new Size(230, 35)
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

        void AddSystemControl(Control control, int bottomMargin = 10)
        {
            control.Margin = new Padding(0, 0, 0, bottomMargin);
            systemLayout.Controls.Add(control);
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

        AddSystemControl(titleLabel, 8);
        AddSystemControl(infoLabel, 10);
        AddSystemControl(adminStatusLabel, 26);
        AddSystemControl(graphicsLabel, 8);
        AddSystemControl(_mpoDisabledCheckBox, 6);
        AddSystemControl(mpoDescriptionLabel, 6);
        AddSystemControl(_mpoStateLabel, 8);
        AddSystemControl(applyMpoButton, 28);
        AddSystemControl(memoryLabel, 8);
        AddSystemControl(_nduCheckBox, 6);
        AddSystemControl(nduDescriptionLabel, 6);
        AddSystemControl(_nduStateLabel, 8);
        AddSystemControl(applyNduButton, 28);
        AddSystemControl(edgeLabel, 8);
        AddSystemControl(_edgeStartupBoostCheckBox, 6);
        AddSystemControl(edgeDescriptionLabel, 6);
        AddSystemControl(_edgeStateLabel, 8);
        AddSystemControl(applyEdgeButton, 28);
        AddSystemControl(repairNetworkLabel, 8);
        AddSystemControl(repairNetworkDescriptionLabel, 8);
        AddSystemControl(repairNetworkButton, 28);
        AddSystemControl(restartAdaptersLabel, 8);
        AddSystemControl(restartAdaptersDescriptionLabel, 8);
        AddSystemControl(restartAdaptersButton, 28);
        AddSystemControl(stabilityLabel, 8);
        AddSystemControl(_dhcpMediaSenseCheckBox, 6);
        AddSystemControl(dhcpMediaSenseDescriptionLabel, 8);
        AddSystemControl(_dhcpMediaSenseStateLabel, 8);
        AddSystemControl(applyDhcpMediaSenseButton, 28);
        AddSystemControl(_googleDnsCheckBox, 6);
        AddSystemControl(googleDnsDescriptionLabel, 8);
        AddSystemControl(_googleDnsStateLabel, 8);
        AddSystemControl(applyGoogleDnsButton, 28);
        AddSystemControl(_disableIpv6CheckBox, 6);
        AddSystemControl(ipv6DescriptionLabel, 8);
        AddSystemControl(_ipv6StateLabel, 8);
        AddSystemControl(applyIpv6Button, 28);
        AddSystemControl(nvidiaOverlayLabel, 8);
        AddSystemControl(_nvidiaOverlayRestartCheckBox, 6);
        AddSystemControl(nvidiaOverlayDescriptionLabel, 8);
        AddSystemControl(_nvidiaOverlayStateLabel, 8);
        AddSystemControl(applyNvidiaOverlayButton, 28);
        AddSystemControl(nvidiaOverlayPreLaunchLabel, 8);
        AddSystemControl(nvidiaOverlayPreLaunchDescriptionLabel, 8);
        AddSystemControl(nvidiaOverlayLaunchChoicesPanel, 0);
        AddSystemControl(nvidiaOverlayCustomProgramPanel, 8);
        AddSystemControl(_nvidiaOverlayPreLaunchStateLabel, 8);
        AddSystemControl(applyNvidiaOverlayPreLaunchButton, 0);
        systemPage.Controls.Add(systemLayout);

        gameModePage.Controls.Add(gameModeLabel);
        gameModePage.Controls.Add(gameModeDescriptionLabel);
        gameModePage.Controls.Add(safeProfileButton);
        gameModePage.Controls.Add(gameProfileButton);
        gameModePage.Controls.Add(experimentalProfileButton);
        gameModePage.Controls.Add(applyGameTweaksButton);
        gameModePage.Controls.Add(restoreRegistryBackupButton);
        gameModePage.Controls.Add(_gameTweaksPanel);

        gameModePage.AutoScrollMinSize = new Size(0, _gameTweaksPanel.Bottom + 24);

        tabControl.TabPages.Add(systemPage);
        tabControl.TabPages.Add(gameModePage);
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

            await Task.WhenAll(
                LoadIpv6StateAsync(),
                LoadNvidiaOverlayRestartStateAsync());
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

        _nvidiaOverlayPreLaunchStateLabel.Text = "NVIDIA Overlay будет автоперезапускаться для: " + string.Join(", ", names);
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

    private void PopulateGameTweaksPanel()
    {
        _gameTweakCheckBoxes.Clear();
        _gameTweaksPanel.Controls.Clear();

        var y = 0;
        var rowWidth = Math.Max(520, _gameTweaksPanel.ClientSize.Width - 8);
        var descriptionWidth = Math.Max(260, rowWidth - 44);
        var descriptionFont = new Font("Segoe UI", 9.5F);
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

            var checkBox = new CheckBox
            {
                Text = tweak.Name,
                Location = new Point(0, 4),
                Size = new Size(rowWidth - 8, 25),
                AutoSize = false,
                UseMnemonic = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent
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

            _gameTweakCheckBoxes[tweak] = checkBox;
            rowPanel.Controls.Add(checkBox);
            rowPanel.Controls.Add(descriptionLabel);
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
            "Get-Process -Name 'NVIDIA Overlay' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue",
            "Start-Sleep -Milliseconds 800",
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
            "$definition.RegistrationInfo.Description = 'Перезапускает NVIDIA Overlay при входе в Windows и после выхода из сна.'",
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
            ShowStatus("Удаляется автоперезапуск Overlay для программ...", Color.Gray);
            var removeResult = await RunElevatedPowerShellAsync(BuildNvidiaOverlayProcessRemoveTaskScript());
            if (removeResult == ElevatedCommandResult.Success)
            {
                ShowStatus("Автоперезапуск Overlay для программ отключён", Color.Green);
                return;
            }

            ShowStatus(removeResult == ElevatedCommandResult.Cancelled ? "Операция отменена" : "Не удалось отключить автоперезапуск Overlay", Color.Orange);
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
            ShowStatus("Не удалось подготовить автоперезапуск Overlay", Color.Orange);
            return;
        }

        if (!IsNvidiaOverlayAvailable())
        {
            var dialogResult = MessageBox.Show(
                "NVIDIA App не найдена по стандартным путям. Создать задачу автоперезапуска всё равно?",
                "NVIDIA Overlay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (dialogResult != DialogResult.Yes)
                return;
        }

        ShowStatus("Создаётся автоперезапуск Overlay для выбранных программ...", Color.Gray);
        var commandResult = await RunElevatedPowerShellAsync(BuildNvidiaOverlayProcessRegisterTaskScript(resolveResult.Targets));

        if (commandResult == ElevatedCommandResult.Success)
        {
            UpdateNvidiaOverlayPreLaunchState();
            ShowStatus("Автоперезапуск Overlay для выбранных программ включён", Color.Green);
            return;
        }

        ShowStatus(commandResult == ElevatedCommandResult.Cancelled ? "Операция отменена" : "Не удалось включить автоперезапуск Overlay", Color.Orange);
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
            "Get-Process -Name 'NVIDIA Overlay' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue",
            "Start-Sleep -Milliseconds 800",
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
            "$service = New-Object -ComObject 'Schedule.Service'",
            "$service.Connect()",
            @"$rootFolder = $service.GetFolder('\')",
            @"try { $taskFolder = $service.GetFolder('\' + $taskFolderName) } catch { $taskFolder = $rootFolder.CreateFolder($taskFolderName) }",
            "$definition = $service.NewTask(0)",
            "$definition.RegistrationInfo.Author = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name",
            "$definition.RegistrationInfo.Description = 'Перезапускает NVIDIA Overlay при запуске выбранных игр или программ.'",
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
            "$restartScriptPath = " + QuotePowerShellString(GetNvidiaOverlayProcessRestartScriptPath()),
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

    private static string BuildProcessCreationEventSubscription(IEnumerable<string> processPaths)
    {
        var conditions = string.Join(" or ", processPaths.Select(path => "Data[@Name='NewProcessName']=" + QuoteEventXPathString(path)));
        return "<QueryList><Query Id=\"0\" Path=\"Security\"><Select Path=\"Security\">*[System[Provider[@Name='Microsoft-Windows-Security-Auditing'] and EventID=4688]] and *[EventData[" + conditions + "]]</Select></Query></QueryList>";
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
        await Task.Delay(2500);
        if (messageVersion != _statusMessageVersion)
            return;

        _statusLabel.Text = string.Empty;
        _statusLabel.Visible = false;
    }

    private sealed record RegistryGameTweak(
        string Name,
        string Description,
        bool Recommended,
        IReadOnlyList<RegistryGameValue> Values);

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
