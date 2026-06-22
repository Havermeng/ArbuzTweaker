using System.Windows.Forms;

namespace ArbuzTweaker;

internal static class UnsafeTweaksPrompt
{
    public const string Marker = "[Небезопасно]";

    public static bool ConfirmEnable(IWin32Window owner, string actionDescription)
    {
        var result = MessageBox.Show(
            "Вы собираетесь разрешить небезопасные системные твики.\n\n" +
            "Такие действия могут менять реестр Windows, сетевые параметры, задачи планировщика или системные службы. " +
            "Неверное применение может временно сломать сеть, вызвать нестабильность или потребовать откат через бэкап/перезагрузку.\n\n" +
            $"Действие: {actionDescription}\n\n" +
            "Если продолжить, безопасный режим будет отключён и выбор сохранится, пока вы сами снова не включите безопасный режим в настройках.",
            "Небезопасные твики",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
    }
}
