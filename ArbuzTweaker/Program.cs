namespace ArbuzTweaker;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\ArbuzTweaker.SingleInstance";
    private const int SwRestore = 9;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        using var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            TryActivateExistingInstance();
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => ReportUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                LogUnhandledException(exception);
        };

        try
        {
            Application.Run(new Form1());
        }
        finally
        {
            singleInstanceMutex.ReleaseMutex();
        }
    }

    private static void ReportUnhandledException(Exception exception)
    {
        LogUnhandledException(exception);

        try
        {
            MessageBox.Show(
                $"Произошла непредвиденная ошибка:\n\n{exception.Message}\n\nПодробности записаны в журнал.",
                "ArbuzTweaker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }

    private static void LogUnhandledException(Exception exception)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArbuzTweaker",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "arbuz-tweaker.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] Unhandled exception: {exception}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static bool TryActivateExistingInstance()
    {
        try
        {
            using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName))
            {
                using (process)
                {
                    if (process.Id == currentProcess.Id || process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    ShowWindow(process.MainWindowHandle, SwRestore);
                    SetForegroundWindow(process.MainWindowHandle);
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
