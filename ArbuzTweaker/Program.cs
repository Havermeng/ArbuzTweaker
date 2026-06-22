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

        if (TryActivateExistingInstance())
        {
            singleInstanceMutex.ReleaseMutex();
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new Form1());
        }
        finally
        {
            singleInstanceMutex.ReleaseMutex();
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
