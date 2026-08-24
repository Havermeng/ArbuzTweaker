using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ArbuzTweaker;

/// <summary>
/// Каталог твиков оптимизации Windows. Каждый твик обратим: прежние значения уходят в бэкап реестра.
/// </summary>
public enum PcTuningCategory
{
    /// <summary>Влияет на FPS и задержку ввода.</summary>
    Latency,

    /// <summary>Против случайных фризов и фоновой активности, не про средний FPS.</summary>
    Stability,

    /// <summary>Приватность и чистота фона. На производительность не влияет.</summary>
    Privacy,

    /// <summary>Даёт прирост, но снижает защиту системы или конфликтует с анти-читами.</summary>
    Risky
}

/// <summary>Честная оценка влияния — показывается пользователю, чтобы не выдавать приватность за FPS.</summary>
public enum PcTuningImpact
{
    /// <summary>Влияние на производительность подтверждено измерениями.</summary>
    Measured,

    /// <summary>Помогает только если проблема есть (микрофризы, засыпание устройств).</summary>
    Situational,

    /// <summary>На производительность не влияет — польза в другом.</summary>
    NoPerformance
}

public enum PcTuningRoot
{
    CurrentUser,
    LocalMachine
}

public sealed record PcTuningValue(
    PcTuningRoot Root,
    string KeyPath,
    string Name,
    object EnabledValue,
    object? DisabledValue,
    RegistryValueKind ValueKind)
{
    public bool Matches(object? currentValue)
    {
        if (ValueKind == RegistryValueKind.DWord)
            return currentValue is int currentInt && EnabledValue is int enabledInt && currentInt == enabledInt;

        if (currentValue is string currentString && EnabledValue is string enabledString)
            return string.Equals(currentString, enabledString, StringComparison.OrdinalIgnoreCase);

        return Equals(currentValue, EnabledValue);
    }

    public static PcTuningValue Machine(string keyPath, string name, int enabledValue, int? disabledValue)
    {
        return new PcTuningValue(PcTuningRoot.LocalMachine, keyPath, name, enabledValue, disabledValue, RegistryValueKind.DWord);
    }

    public static PcTuningValue User(string keyPath, string name, int enabledValue, int? disabledValue)
    {
        return new PcTuningValue(PcTuningRoot.CurrentUser, keyPath, name, enabledValue, disabledValue, RegistryValueKind.DWord);
    }

    public static PcTuningValue MachineString(string keyPath, string name, string enabledValue, string? disabledValue)
    {
        return new PcTuningValue(PcTuningRoot.LocalMachine, keyPath, name, enabledValue, disabledValue, RegistryValueKind.String);
    }

    public static PcTuningValue UserString(string keyPath, string name, string enabledValue, string? disabledValue)
    {
        return new PcTuningValue(PcTuningRoot.CurrentUser, keyPath, name, enabledValue, disabledValue, RegistryValueKind.String);
    }
}

/// <summary>Действия, которые нельзя выразить одной записью в реестр.</summary>
public enum PcTuningAction
{
    None,

    /// <summary>powercfg /h off — снимает гибернацию, Fast Startup и удаляет hiberfil.sys.</summary>
    Hibernation,

    /// <summary>Снимает «Разрешить отключение устройства для экономии энергии» у всех устройств (WMI).</summary>
    DevicePowerSaving,

    /// <summary>NetBIOS over TCP/IP по всем интерфейсам.</summary>
    NetBios,

    /// <summary>DisableDynamicPstate=1 у всех видеокарт NVIDIA (подключ Class выбирается автоматически).</summary>
    NvidiaPState,

    /// <summary>compact /compactos:always — сжимает системные файлы Windows, освобождает место на SSD.</summary>
    CompactOs,

    /// <summary>Схема питания «Максимальная производительность» (powercfg -duplicatescheme + активация).</summary>
    UltimatePlan,

    /// <summary>Распарковка ядер CPU — минимум активных ядер 100% в текущей схеме питания.</summary>
    CoreUnpark,

    /// <summary>Алгоритм Нагла off — TcpAckFrequency=1 и TCPNoDelay=1 по всем интерфейсам.</summary>
    Nagle,

    /// <summary>Отключение энергосбережения и модерации прерываний у сетевых адаптеров (Green/EEE/FlowControl/InterruptModeration).</summary>
    NicOffloads,

    /// <summary>Минимальное состояние процессора 100% + агрессивный режим ускорения в текущей схеме питания (powercfg).</summary>
    MaxCpuPerformance
}

public sealed record PcTuningTweak(
    string Id,
    string Name,
    string Description,
    PcTuningCategory Category,
    PcTuningImpact Impact,
    IReadOnlyList<PcTuningValue> Values)
{
    /// <summary>Минимальный номер сборки Windows; 0 — без ограничений.</summary>
    public int MinBuild { get; init; }

    /// <summary>Предупреждение, которое пользователь обязан увидеть до применения.</summary>
    public string? Warning { get; init; }

    public bool RequiresReboot { get; init; }

    public PcTuningAction Action { get; init; } = PcTuningAction.None;

    public bool RequiresAdmin
    {
        get
        {
            if (Action != PcTuningAction.None)
                return true;

            foreach (var value in Values)
            {
                if (value.Root == PcTuningRoot.LocalMachine)
                    return true;
            }

            return false;
        }
    }

    public bool IsSupported(int currentBuild) => MinBuild == 0 || currentBuild >= MinBuild;
}

public static class PcTuningCatalog
{
    private const string DataCollectionPath = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    private const string PoliciesSystemPath = @"SOFTWARE\Policies\Microsoft\Windows\System";
    private const string WindowsUpdatePolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string DriverSearchingPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\DriverSearching";
    private const string CloudContentPath = @"SOFTWARE\Policies\Microsoft\Windows\CloudContent";
    private const string FileSystemPath = @"SYSTEM\CurrentControlSet\Control\FileSystem";

    public static IReadOnlyList<PcTuningTweak> All { get; } = new[]
    {
        // ───────────── Задержки и производительность ─────────────
        new PcTuningTweak(
            "device-power-saving",
            "Запретить отключение устройств для экономии энергии",
            "Снимает галку «Разрешить отключение этого устройства для экономии энергии» у всех устройств сразу. Лечит микрофризы и залипания мыши, клавиатуры и сети из-за selective suspend.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.DevicePowerSaving,
            Warning = "Настройка слетает при переподключении устройства — после переподключения примените ещё раз."
        },

        new PcTuningTweak(
            "fault-tolerant-heap",
            "Отключить Fault Tolerant Heap",
            "После пары падений Windows молча навешивает на приложение защитную прослойку FTH, и оно навсегда остаётся медленнее. Твик запрещает такое поведение — известная причина необъяснимых просадок в играх.",
            PcTuningCategory.Latency,
            PcTuningImpact.Measured,
            new[] { PcTuningValue.Machine(@"SOFTWARE\Microsoft\FTH", "Enabled", 0, 1) })
        {
            RequiresReboot = true
        },

        new PcTuningTweak(
            "gamebar-presence-writer",
            "Отключить GameBarPresenceWriter",
            "Убирает постоянно висящий фоновый процесс GameBarPresenceWriter. На сам Game Mode и запись игр не влияет.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            new[]
            {
                PcTuningValue.Machine(
                    @"SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter",
                    "ActivationType",
                    0,
                    1)
            }),

        new PcTuningTweak(
            "transparency-effects",
            "Отключить эффекты прозрачности",
            "Прозрачность интерфейса стоит небольшого, но измеримого времени CPU. Заодно интерфейс становится отзывчивее.",
            PcTuningCategory.Latency,
            PcTuningImpact.Measured,
            new[]
            {
                PcTuningValue.User(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1)
            }),

        new PcTuningTweak(
            "raw-mouse-throttle",
            "Снять троттлинг ввода фоновых окон",
            "Windows 11 ограничивает частоту сообщений мыши для неактивных окон примерно до 125 Гц. Твик ставит минимальный интервал. Влияет только на фоновые окна — на активную игру не действует.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            new[] { PcTuningValue.User(@"Control Panel\Mouse", "RawMouseThrottleDuration", 3, 8) })
        {
            MinBuild = 22621
        },

        // ───────────── Против фризов и фоновой активности ─────────────
        new PcTuningTweak(
            "hibernation",
            "Отключить гибернацию и быстрый запуск",
            "Выполняет powercfg /h off: убирает гибернацию, быстрый запуск и удаляет файл hiberfil.sys (обычно несколько гигабайт). Компьютер начинает выключаться по-настоящему, без переноса старого состояния драйверов в следующий сеанс.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.Hibernation,
            Warning = "После применения пропадёт режим гибернации и быстрый запуск (загрузка станет чуть дольше)."
        },

        new PcTuningTweak(
            "fast-startup",
            "Отключить только быстрый запуск",
            "Оставляет гибернацию, но выключает Fast Startup — режим, из-за которого Windows не выключается полностью и тащит старое состояние ядра и драйверов в новый сеанс.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[]
            {
                PcTuningValue.Machine(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1)
            })
        {
            RequiresReboot = true
        },

        new PcTuningTweak(
            "search-indexing",
            "Отключить службу индексации поиска",
            "Служба Windows Search периодически нагружает диск и процессор фоновой переиндексацией. Отключение убирает эти всплески.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[] { PcTuningValue.Machine(@"SYSTEM\CurrentControlSet\Services\WSearch", "Start", 4, 2) })
        {
            Warning = "Поиск в меню «Пуск» и Проводнике станет заметно медленнее и потеряет мгновенные подсказки.",
            RequiresReboot = true
        },

        new PcTuningTweak(
            "sysmain",
            "Отключить SysMain (Superfetch)",
            "SysMain постоянно читает диск, предугадывая запуск программ. На SSD пользы почти нет, а фоновая активность есть; отключение входит в рекомендации Microsoft для систем реального времени.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[] { PcTuningValue.Machine(@"SYSTEM\CurrentControlSet\Services\SysMain", "Start", 4, 2) })
        {
            Warning = "Включайте только если система стоит на SSD или NVMe. На обычном жёстком диске твик сделает хуже.",
            RequiresReboot = true
        },

        new PcTuningTweak(
            "automatic-maintenance",
            "Отключить автоматическое обслуживание Windows",
            "Windows сама выбирает момент для дефрагментации, проверок и отчётов — обычно самый неудачный. Твик запрещает автозапуск обслуживания.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "MaintenanceDisabled", 1, 0)
            })
        {
            Warning = "TRIM для SSD и дефрагментацию придётся иногда запускать вручную (Оптимизация дисков)."
        },

        new PcTuningTweak(
            "last-access-time",
            "Не обновлять время последнего доступа к файлам",
            "NTFS перестаёт записывать отметку времени при каждом чтении файла или просмотре папки. Небольшая экономия дисковых операций.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[]
            {
                // 0x80000001 — «системой управляемое, выключено», 0x80000000 — значение Windows по умолчанию.
                PcTuningValue.Machine(FileSystemPath, "NtfsDisableLastAccessUpdate", unchecked((int)0x80000001), unchecked((int)0x80000000))
            })
        {
            Warning = "Некоторые программы резервного копирования и удалённого хранилища опираются на это время."
        },

        new PcTuningTweak(
            "8dot3-names",
            "Не создавать короткие имена файлов (8.3)",
            "Отключает создание архаичных коротких имён вида PROGRA~1 для новых файлов. Немного ускоряет операции с каталогами и убирает лишний путь доступа к файлу.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[] { PcTuningValue.Machine(FileSystemPath, "NtfsDisable8dot3NameCreation", 1, 2) }),

        new PcTuningTweak(
            "windows-auto-update",
            "Отключить автоматические обновления Windows",
            "Windows перестаёт сама скачивать и устанавливать обновления с перезагрузками. Проверять обновления вручную по-прежнему можно.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[] { PcTuningValue.Machine(WindowsUpdatePolicyPath + @"\AU", "NoAutoUpdate", 1, 0) })
        {
            Warning = "Обновления безопасности придётся ставить самому — не забывайте про это."
        },

        new PcTuningTweak(
            "wu-drivers",
            "Не ставить драйверы через Windows Update",
            "Запрещает Windows Update подсовывать свои версии драйверов поверх установленных вручную — частая причина отката свежего драйвера видеокарты на старый.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(WindowsUpdatePolicyPath, "ExcludeWUDriversInQualityUpdate", 1, 0),
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 0, 1),
                PcTuningValue.Machine(DriverSearchingPolicyPath, "SearchOrderConfig", 0, null),
                PcTuningValue.Machine(DriverSearchingPolicyPath, "DontSearchWindowsUpdate", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata", "PreventDeviceMetadataFromNetwork", 1, null)
            }),

        new PcTuningTweak(
            "store-auto-update",
            "Отключить автообновление приложений Store",
            "Магазин перестаёт скачивать обновления приложений в фоне, в том числе во время игры.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[] { PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2, null) }),

        new PcTuningTweak(
            "background-apps",
            "Запретить работу приложений в фоне",
            "Отключает фоновую работу приложений из Microsoft Store. В Windows 11 общего переключателя в настройках больше нет — только через политику.",
            PcTuningCategory.Stability,
            PcTuningImpact.Situational,
            new[] { PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2, null) })
        {
            Warning = "Приложения Store перестанут показывать уведомления и обновлять данные в фоне."
        },

        new PcTuningTweak(
            "error-reporting",
            "Отключить отчёты об ошибках Windows",
            "После падения игры Windows не собирает и не отправляет дамп — не будет фоновой работы werfault и окон с отчётами.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, 0),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\PCHealth\ErrorReporting", "DoReport", 0, 1),
                PcTuningValue.User(@"Software\Microsoft\Windows\Windows Error Reporting", "DontSendAdditionalData", 1, null)
            }),

        new PcTuningTweak(
            "qos-no-nla",
            "Разрешить QoS вне доменной сети",
            "Нужен, чтобы политики QoS реально помечали пакеты, когда компьютер не в домене или сетевых адаптеров несколько. Сам по себе ничего не ускоряет — работает только вместе с настроенной политикой QoS.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.MachineString(@"SYSTEM\CurrentControlSet\Services\Tcpip\QoS", "Do not use NLA", "1", null)
            }),

        new PcTuningTweak(
            "netbios",
            "Отключить NetBIOS over TCP/IP",
            "Компьютер перестаёт слушать старые порты 137-139 на всех сетевых адаптерах. Применяется к каждому текущему интерфейсу.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.NetBios,
            Warning = "Может помешать доступу к очень старым сетевым папкам в локальной сети."
        },

        // ───────────── Приватность и фон ─────────────
        new PcTuningTweak(
            "telemetry",
            "Отключить телеметрию",
            "Останавливает службу DiagTrack и запрещает сбор диагностических данных. Минус постоянно работающий фоновый процесс с сетевой активностью.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(@"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2),
                PcTuningValue.Machine(DataCollectionPath, "AllowTelemetry", 0, null),
                PcTuningValue.Machine(DataCollectionPath, "DoNotShowFeedbackNotifications", 1, null),
                PcTuningValue.Machine(DataCollectionPath, "LimitDiagnosticLogCollection", 1, null),
                PcTuningValue.Machine(DataCollectionPath, "LimitDumpCollection", 1, null)
            })
        {
            RequiresReboot = true
        },

        new PcTuningTweak(
            "cloud-content",
            "Отключить рекламный контент Windows",
            "Windows перестаёт сама доустанавливать рекомендованные приложения, показывать подсказки и рекламные предложения на экране блокировки и в меню «Пуск».",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(CloudContentPath, "DisableWindowsConsumerFeatures", 1, null),
                PcTuningValue.Machine(CloudContentPath, "DisableCloudOptimizedContent", 1, null),
                PcTuningValue.Machine(CloudContentPath, "DisableConsumerAccountStateContent", 1, null),
                PcTuningValue.Machine(CloudContentPath, "DisableSoftLanding", 1, null)
            }),

        new PcTuningTweak(
            "search-web-results",
            "Убрать веб-подсказки из поиска Windows",
            "Поиск в меню «Пуск» перестаёт обращаться в интернет и ищет только по компьютеру — быстрее и без отправки запросов наружу.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, null),
                PcTuningValue.User(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null)
            }),

        new PcTuningTweak(
            "activity-clipboard",
            "Отключить журнал буфера обмена и историю действий",
            "Windows перестаёт хранить историю буфера обмена и отправлять журнал действий в облако.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(PoliciesSystemPath, "AllowClipboardHistory", 0, null),
                PcTuningValue.Machine(PoliciesSystemPath, "AllowCrossDeviceClipboard", 0, null),
                PcTuningValue.Machine(PoliciesSystemPath, "EnableActivityFeed", 0, null),
                PcTuningValue.Machine(PoliciesSystemPath, "PublishUserActivities", 0, null),
                PcTuningValue.Machine(PoliciesSystemPath, "UploadUserActivities", 0, null)
            })
        {
            Warning = "Перестанет работать вызов истории буфера обмена по Win+V."
        },

        new PcTuningTweak(
            "advertising-id",
            "Отключить рекламный идентификатор и подсказки ввода",
            "Приложения перестают получать рекламный идентификатор, а Windows — собирать статистику набора текста.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, null),
                PcTuningValue.User(@"SOFTWARE\Microsoft\input\Settings", "InsightsEnabled", 0, null),
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\TextInput", "AllowLinguisticDataCollection", 0, null)
            }),

        new PcTuningTweak(
            "widgets",
            "Отключить виджеты и новости",
            "Убирает панель виджетов с новостями и погодой вместе с её фоновым процессом.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[] { PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, null) }),

        new PcTuningTweak(
            "sticky-keys",
            "Отключить залипание клавиш",
            "Убирает окно залипания клавиш, которое выскакивает от пяти нажатий Shift — обычно прямо посреди игры.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[] { PcTuningValue.UserString(@"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510") }),

        new PcTuningTweak(
            "autoplay",
            "Отключить автозапуск съёмных носителей",
            "Windows перестаёт автоматически запускать программы с флешек и дисков. Чистая мера безопасности.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255, null),
                PcTuningValue.Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoAutorun", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", "NoAutoplayfornonVolume", 1, null)
            }),

        new PcTuningTweak(
            "file-extensions",
            "Показывать расширения файлов",
            "Проводник перестаёт прятать расширения — видно, что «фото.jpg.exe» на самом деле программа.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.User(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1)
            }),

        // ───────────── Даёт прирост, но снижает защиту / трогает анти-читы ─────────────
        new PcTuningTweak(
            "nvidia-pstate-0",
            "NVIDIA: фиксировать P-State 0 (макс. частота GPU)",
            "Запрещает видеокарте NVIDIA сбрасывать частоты в лёгких сценах (DisableDynamicPstate). Стабильнее фреймтайм там, где GPU иначе засыпал бы. Нужный подключ драйвера выбирается автоматически.",
            PcTuningCategory.Risky,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.NvidiaPState,
            RequiresReboot = true,
            Warning = "Выше температура и потребление GPU в простое. Только для видеокарт NVIDIA."
        },

        new PcTuningTweak(
            "memory-integrity-off",
            "Отключить Memory Integrity (HVCI)",
            "Снимает аппаратную проверку целостности кода на уровне виртуализации — единственный твик из группы с подтверждённым замерами приростом FPS в CPU-сценах. Для Dota 2 и SCP:SL по анти-читам безопасно.",
            PcTuningCategory.Risky,
            PcTuningImpact.Measured,
            new[]
            {
                PcTuningValue.Machine(
                    @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                    "Enabled",
                    0,
                    1)
            })
        {
            RequiresReboot = true,
            Warning = "Снижает защиту системы. Требуют включённым Valorant/Vanguard и FACEIT — там игра не запустится. Может включиться обратно, если в BIOS включена виртуализация."
        },

        new PcTuningTweak(
            "vbs-off",
            "Отключить Virtualization Based Security (VBS)",
            "Отключает слой виртуализации безопасности — тот же оверхед, что и у Memory Integrity. Прирост FPS там, где VBS был активен.",
            PcTuningCategory.Risky,
            PcTuningImpact.Measured,
            new[]
            {
                PcTuningValue.Machine(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0, 1)
            })
        {
            RequiresReboot = true,
            Warning = "Снижает защиту системы и ломает WSL/песочницу Windows. Не отключится, пока включены Hyper-V, Memory Integrity или платформа виртуальных машин."
        },

        new PcTuningTweak(
            "defender-realtime-off",
            "Отключить защиту в реальном времени Defender",
            "Ставит политики, отключающие постоянное сканирование Windows Defender. Убирает реальный оверхед CPU/диска при загрузке карт, распаковке и записи реплеев на слабых процессорах.",
            PcTuningCategory.Risky,
            PcTuningImpact.Measured,
            new[]
            {
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableBehaviorMonitoring", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableScanOnRealtimeEnable", 1, null),
                PcTuningValue.Machine(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableOnAccessProtection", 1, null)
            })
        {
            Warning = "Оставляет систему без антивируса. Сработает только при выключенной Tamper Protection (её нужно снять вручную в «Безопасность Windows»). Windows может вернуть защиту при обновлении."
        },

        // ───────────── Добавлено из внешних гайдов (TikTok/Steam) ─────────────
        new PcTuningTweak(
            "filter-keys",
            "Отключить фильтрацию ввода (Filter Keys)",
            "Filter Keys включается удержанием правого Shift 8 секунд и заставляет Windows игнорировать быстрые повторные нажатия. Из-за него в игре могут «теряться» нажатия и выскакивать окно спец-возможностей. Твик выключает этот режим и запрет на его случайное включение.",
            PcTuningCategory.Privacy,
            PcTuningImpact.NoPerformance,
            new[]
            {
                PcTuningValue.UserString(@"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126")
            }),

        new PcTuningTweak(
            "compact-os",
            "Сжать системные файлы (CompactOS)",
            "Включает компактное сжатие системных файлов Windows (compact /compactos). Освобождает несколько гигабайт на SSD без заметной потери скорости.",
            PcTuningCategory.Stability,
            PcTuningImpact.NoPerformance,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.CompactOs,
            Warning = "Первое сжатие может занять несколько минут."
        },

        new PcTuningTweak(
            "nagle-off",
            "Отключить алгоритм Нагла",
            "Ставит TcpAckFrequency=1 и TCPNoDelay=1 всем сетевым интерфейсам — пакеты уходят без задержки на объединение. Может снизить сетевую задержку в онлайн-играх.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.Nagle
        },

        new PcTuningTweak(
            "nic-offloads",
            "Отключить энергосбережение сетевой карты",
            "Выключает у сетевых адаптеров Green Ethernet, Energy Efficient Ethernet, Flow Control и Interrupt Moderation — функции, которые ради экономии добавляют задержку пакетам.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.NicOffloads,
            RequiresReboot = true
        },

        new PcTuningTweak(
            "ultimate-plan",
            "План питания «Максимальная производительность»",
            "Разблокирует и включает скрытую схему питания Ultimate Performance — процессор реже сбрасывает частоты. Эффект спорный и зависит от системы, у части ПК его нет.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.UltimatePlan,
            Warning = "Выше энергопотребление и нагрев. На ноутбуках быстрее садит батарею."
        },

        new PcTuningTweak(
            "max-cpu-performance",
            "Максимальная частота процессора",
            "Ставит минимальное состояние процессора на 100% и режим ускорения «Агрессивный» в текущей схеме питания. Процессор перестаёт снижать частоту в простое — заметно ровнее становятся редкие просадки (1% low), игра ощущается плавнее.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.MaxCpuPerformance,
            Warning = "Выше энергопотребление и нагрев, кулер шумит и в простое. На ноутбуках заметно быстрее садится батарея. Откат возвращает минимальное состояние к 5%."
        },

        new PcTuningTweak(
            "core-unpark",
            "Распарковать ядра процессора",
            "Запрещает Windows парковать (усыплять) ядра CPU в текущей схеме питания — все ядра всегда активны. Может убрать микрофризы на переходах нагрузки.",
            PcTuningCategory.Latency,
            PcTuningImpact.Situational,
            Array.Empty<PcTuningValue>())
        {
            Action = PcTuningAction.CoreUnpark,
            Warning = "Выше энергопотребление и нагрев в простое."
        }
    };
}
