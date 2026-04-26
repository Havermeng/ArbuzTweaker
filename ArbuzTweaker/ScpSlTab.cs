using System.Windows.Forms;

namespace ArbuzTweaker;

public partial class ScpSlTab : UserControl
{
    private readonly ScpSlService _scpSlService;

    public ScpSlTab()
    {
        _scpSlService = new ScpSlService();
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

        var bootConfigPage = new TabPage
        {
            Text = "boot.config",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        var commandBindingsPage = new TabPage
        {
            Text = "Бинды команд",
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };

        var launchOptionsTab = new ScpSlLaunchOptionsTab(_scpSlService)
        {
            Dock = DockStyle.Fill
        };

        var bootConfigTab = new ScpSlBootConfigTab(_scpSlService)
        {
            Dock = DockStyle.Fill
        };

        var commandBindingsTab = new ScpSlCommandBindingsTab(_scpSlService)
        {
            Dock = DockStyle.Fill
        };

        launchOptionsPage.Controls.Add(launchOptionsTab);
        bootConfigPage.Controls.Add(bootConfigTab);
        commandBindingsPage.Controls.Add(commandBindingsTab);

        tabControl.TabPages.Add(launchOptionsPage);
        tabControl.TabPages.Add(bootConfigPage);
        tabControl.TabPages.Add(commandBindingsPage);

        Controls.Add(tabControl);
    }
}
