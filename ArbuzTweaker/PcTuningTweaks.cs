using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ArbuzTweaker;

/// <summary>
/// Твики из гайда PC-Tuning (github.com/valleyofdoom/PC-Tuning), адаптированные под твикер.
/// В отличие от оригинала каждый твик обратим: прежние значения уходят в бэкап реестра.
/// </summary>
public enum PcTuningCategory
{
    /// <summary>Влияет на FPS и задержку ввода.</summary>
    Latency,

    /// <summary>Против случайных фризов и фоновой активности, не про средний FPS.</summary>
    Stability,

    /// <summary>Приватность и чистота фона. На производительность не влияет.</summary>
    Privacy
}

/// <summary>Честная оценка влияния — показывается пользователю, чтобы не выдавать приватность за FPS.</summary>
public enum PcTuningImpact
{
    /// <summary>Влияние подтверждено измерениями автора гайда.</summary>
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
    NetBios
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
            "Убирает постоянно висящий фоновый процесс GameBarPresenceWriter. На сам Game Mode и запись игр не влияет — это проверено автором гайда отдельно.",
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
            "Прозрачность интерфейса стоит небольшого, но измеримого времени CPU — у автора гайда есть замер. Заодно интерфейс становится отзывчивее.",
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
            })
    };
}
