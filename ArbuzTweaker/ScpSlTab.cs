namespace ArbuzTweaker;

public partial class ScpSlTab : UserControl
{
    private readonly ScpSlService _scpSlService;
    private readonly AppSettingsService _appSettingsService;

    public ScpSlTab(ScpSlService scpSlService, AppSettingsService appSettingsService)
    {
        _scpSlService = scpSlService;
        _appSettingsService = appSettingsService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var launchOptionsPage = new TabPage
        {
            Text = "Параметры запуска",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        var commandBindingsPage = new TabPage
        {
            Text = "Бинды команд",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        launchOptionsPage.Controls.Add(new ScpSlLaunchOptionsTab(_scpSlService, _appSettingsService)
        {
            Dock = DockStyle.Fill
        });

        commandBindingsPage.Controls.Add(new ScpSlCommandBindingsTab(_scpSlService)
        {
            Dock = DockStyle.Fill
        });

        tabControl.TabPages.Add(launchOptionsPage);
        tabControl.TabPages.Add(commandBindingsPage);

        Controls.Add(tabControl);
    }
}
