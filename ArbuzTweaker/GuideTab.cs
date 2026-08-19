using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArbuzTweaker;

/// <summary>
/// Вкладка «Гайд» — краткое руководство по программе в едином стиле с остальными вкладками.
/// </summary>
public sealed class GuideTab : UserControl
{
    private static readonly Font TitleFont = new("Segoe UI", 15F, FontStyle.Bold);
    private static readonly Font ParagraphFont = new("Segoe UI", 10F);
    private static readonly Font ItemTitleFont = new("Segoe UI Semibold", 10F);

    public GuideTab()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        AutoScroll = true;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(24, 18, 24, 28),
            BackColor = UiTheme.Surface
        };

        var wrap = new List<Control>();

        void Title(string text)
        {
            layout.Controls.Add(new Label
            {
                Text = text,
                Font = TitleFont,
                ForeColor = UiTheme.TextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            });
        }

        void Header(string text)
        {
            var label = UiTheme.CreateSectionHeaderLabel(text);
            label.Margin = new Padding(0, 18, 0, 8);
            layout.Controls.Add(label);
        }

        void Para(string text)
        {
            var label = new Label
            {
                Text = text,
                Font = ParagraphFont,
                ForeColor = UiTheme.TextMuted,
                AutoSize = true,
                MaximumSize = new Size(860, 0),
                Margin = new Padding(0, 0, 0, 8)
            };
            layout.Controls.Add(label);
            wrap.Add(label);
        }

        void Item(string title, string text)
        {
            layout.Controls.Add(new Label
            {
                Text = title,
                Font = ItemTitleFont,
                ForeColor = UiTheme.TextPrimary,
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 2)
            });
            var label = new Label
            {
                Text = text,
                Font = ParagraphFont,
                ForeColor = UiTheme.TextMuted,
                AutoSize = true,
                MaximumSize = new Size(860, 0),
                Margin = new Padding(12, 0, 0, 8)
            };
            layout.Controls.Add(label);
            wrap.Add(label);
        }

        Title("Как пользоваться ArbuzTweaker");
        Para("Программа собирает в одном месте твики и настройки для игр и Windows. Большинство системных "
            + "твиков требуют запуска от имени администратора — тогда в шапке вкладки «Windows» будет зелёная "
            + "строка о правах. Меняйте по одному пункту за раз и проверяйте результат: универсальных твиков нет, "
            + "эффект зависит от конкретного ПК.");

        Header("Главное про безопасность");
        Para("Каждое изменение реестра сначала уходит в бэкап, поэтому его можно откатить кнопкой «Откатить "
            + "бэкап реестра». Твики с пометкой «[Небезопасно]» и раздел «Рискованные» на вкладке «Оптимизация» "
            + "могут снижать защиту системы или конфликтовать с анти-читами — читайте предупреждения под пунктом.");

        Header("Маркеры влияния");
        Para("У каждого твика справа стоит честная оценка того, на что он влияет:");
        Item("+FPS (зелёный)", "Реально поднимает частоту кадров или снижает задержку ввода.");
        Item("против фризов (синий)", "Помогает от подтормаживаний, лагов, утечек памяти и сетевых просадок — если такая проблема есть.");
        Item("чистит фон (серый)", "На FPS не влияет. Польза в другом: меньше фоновых процессов, приватность, удобство.");

        Header("Вкладка «Windows»");
        Item("Система", "Точечные системные твики (графика, память, сеть, NVIDIA Overlay, приоритет игр). "
            + "У каждого своя кнопка «Применить» и строка текущего состояния — удобно включать по одному.");
        Item("Игровой режим", "Набор игровых твиков реестра. Выберите готовый профиль (безопасный / игровой / "
            + "экспериментальный) или отметьте пункты вручную, затем нажмите «Применить игровые твики». "
            + "Безопасный профиль трогает только пользовательские (HKCU) параметры и не требует администратора.");
        Item("Оптимизация", "Список твиков с галочками. Отметьте нужные (или нажмите «Отметить безопасные»), "
            + "затем «Применить». Есть поиск по названию и откат. Всё применяется одним нажатием и уходит в бэкап.");

        Header("Вкладки Dota 2 и SCP:SL");
        Para("Здесь настраиваются параметры запуска Steam, autoexec и видео-конфиг игр. Если Steam запущен, "
            + "твикер предложит его закрыть перед изменением параметров запуска — иначе Steam перезапишет их при "
            + "выходе. Можно выбрать конкретный Steam-аккаунт, если их несколько.");

        Header("Прицел");
        Para("Рисует пользовательский прицел поверх игры отдельным окном. Настройте вид, сохраните пресет и "
            + "включите оверлей. Если прицел не виден поверх игры — запустите твикер от имени администратора.");

        Header("Функции и Стороннее ПО");
        Item("Функции", "Быстрые действия, которые не меняют системные настройки: перезапуск Проводника "
            + "(лечит панель задач и значки после сна) и отключение экрана.");
        Item("Стороннее ПО", "Быстрый доступ к внешним утилитам (NVIDIA Inspector, MSI Afterburner, Intel XTU) "
            + "и их профилям, если они установлены.");

        Header("Настройки");
        Para("Проверка обновлений, управление бэкапами реестра и файлов, профили настроек и сброс показанных "
            + "предупреждений. Обновление скачивается с проверкой контрольной суммы и подписи.");

        Header("Если написало «Применено 0, с ошибкой 1»");
        Para("Обычно это значит, что нужный раздел реестра принадлежит системе (TrustedInstaller) и запрещён "
            + "для записи, либо не хватает прав администратора. Запустите твикер от имени администратора и "
            + "попробуйте снова. Отдельные защищённые ключи твикер применяет, временно забирая их во владение и "
            + "возвращая права как было.");

        Controls.Add(layout);
        UiTheme.EnableDynamicLabelWrap(layout, wrap.ToArray());
    }
}
