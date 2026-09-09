using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using LexPercent.Core;
using LexPercent.Domain;
using LexPercent.Storage;
using System.Globalization;
namespace LexPercent.UI;

public sealed partial class MainWindow
{
    async Task CompareVersions()
    {
        var dialog = new Window { Title = "Сравнение версий", Width = 650, Height = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner }; var a = new ComboBox { ItemsSource = file.Versions.Select(v => v.Number).ToArray(), SelectedIndex = 0 }; var b = new ComboBox { ItemsSource = file.Versions.Select(v => v.Number).ToArray(), SelectedIndex = Math.Max(0, file.Versions.Count - 1) }; var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 300 };
        void Compare() { if (a.SelectedItem is int x && b.SelectedItem is int y) { var one = file.Versions.Single(v => v.Number == x); var two = file.Versions.Single(v => v.Number == y); text.Text = ProjectStore.Compare(one.Project, two.Project) + $"\nПроценты: {one.Result?.Interest:N2} → {two.Result?.Interest:N2}\nДни: {one.Result?.Days} → {two.Result?.Days}"; } }
        a.SelectionChanged += (_, _) => Compare(); b.SelectionChanged += (_, _) => Compare(); dialog.Content = new StackPanel { Margin = new(20), Spacing = 12, Children = { a, b, text } }; Compare(); await dialog.ShowDialog(this);
    }
    async Task Settings(bool first)
    {
        var dialog = new Window { Title = first ? "Добро пожаловать в LexPercent Pro" : "Настройки", Width = 580, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner }; var panel = new StackPanel { Margin = new(24), Spacing = 10 }; panel.Children.Add(new TextBlock { Text = first ? "Укажите реквизиты для новых проектов" : "Общие настройки", FontSize = 20 });
        TextBox Add(string name, string text) { panel.Children.Add(new TextBlock { Text = name }); var t = new TextBox { Text = text }; panel.Children.Add(t); return t; }
        var org = Add("Организация", settings.Organization); var mgr = Add("Руководитель", settings.Manager); var dir = Add("Каталог проектов", settings.ProjectDirectory); var interval = Add("Автосохранение, минут", settings.AutosaveMinutes.ToString()); var approval = new CheckBox { Content = "Добавлять блок «УТВЕРЖДАЮ»", IsChecked = settings.Document.ShowApproval }; panel.Children.Add(approval); var err = new TextBlock { Foreground = Brushes.DarkRed }; panel.Children.Add(err); var save = new Button { Content = "Сохранить настройки" };
        save.Click += (_, _) => { if (!int.TryParse(interval.Text, out int minutes) || minutes < 1 || minutes > 120) { err.Text = "Укажите интервал от 1 до 120 минут."; return; } try { Directory.CreateDirectory(dir.Text ?? ""); settings = settings with { Initialized = true, Organization = org.Text ?? "", Manager = mgr.Text ?? "", ProjectDirectory = dir.Text!, AutosaveMinutes = minutes, Document = settings.Document with { ShowApproval = approval.IsChecked == true } }; SettingsStore.Save(settings); autosave.Interval = TimeSpan.FromMinutes(minutes); if (first) { file = new() { Project = Blank() }; Render(); } dialog.Close(); } catch (Exception ex) { err.Text = ex.Message; } };
        var signatories = new Button { Content = "Справочник подписантов" };
        signatories.Click += async (_, _) => await EditSignatories(dialog); panel.Children.Add(signatories);
        panel.Children.Add(save); dialog.Content = panel; await dialog.ShowDialog(this);
    }
    async Task Formatting()
    {
        var dialog = new Window { Title = "Оформление документа", Width = 780, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner }; var panel = new StackPanel { Margin = new(20), Spacing = 10 }; var options = file.Project.Document; var approval = new CheckBox { Content = "Блок «УТВЕРЖДАЮ»", IsChecked = options.ShowApproval }; var landscape = new CheckBox { Content = "Альбомная ориентация A4", IsChecked = options.Landscape }; var margin = new TextBox { Text = options.MarginMm.ToString(CultureInfo.InvariantCulture), PlaceholderText = "Поля, мм" }; panel.Children.Add(approval); panel.Children.Add(landscape); panel.Children.Add(new TextBlock { Text = "Поля страницы, мм" }); panel.Children.Add(margin);
        panel.Children.Add(new TextBlock { Text = "Текст блока утверждения (переносы строк сохраняются)" });
        var approvalText = new TextBox { Text = options.ApprovalText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140 };
        panel.Children.Add(approvalText);
        var styles = new List<Func<BlockStyle>>(); foreach (var (name, st) in new[] { ("Заголовок", options.Title), ("Основной текст", options.Body), ("Таблица", options.Table), ("Утверждение", options.Approval), ("Итог", options.Summary), ("Подпись", options.Signature) })
        {
            panel.Children.Add(new TextBlock { Text = name, FontWeight = FontWeight.Bold }); var row = new WrapPanel(); var font = new ComboBox { ItemsSource = new[] { "Times New Roman", "Arial", "Courier New", "Segoe UI" }, SelectedItem = st.Font, Width = 160 }; var size = new TextBox { Text = N(st.Size), Width = 55 }; var bold = new CheckBox { Content = "Ж", IsChecked = st.Bold }; var italic = new CheckBox { Content = "К", IsChecked = st.Italic }; var align = new ComboBox { ItemsSource = new[] { "left", "center", "right" }, SelectedItem = st.Align, Width = 100 }; var before = new TextBox { Text = N(st.Before), PlaceholderText = "До", Width = 50 }; var after = new TextBox { Text = N(st.After), PlaceholderText = "После", Width = 50 }; var line = new TextBox { Text = N(st.LineSpacing), Width = 50 }; var indent = new TextBox { Text = N(st.Indent), Width = 50 };
            var color = new TextBox { Text = st.Color, Width = 90 };
            foreach (var c in new Control[] { font, size, bold, italic, align, before, after, line, indent, color }) { c.Margin = new(0, 0, 6, 0); row.Children.Add(c); }
            panel.Children.Add(row);
            styles.Add(() => new() { Font = font.SelectedItem?.ToString() ?? "Times New Roman", Color = color.Text ?? "#000000", Size = (double)Amount(size.Text ?? ""), Bold = bold.IsChecked == true, Italic = italic.IsChecked == true, Align = align.SelectedItem?.ToString() ?? "left", Before = (double)Amount(before.Text ?? ""), After = (double)Amount(after.Text ?? ""), LineSpacing = (double)Amount(line.Text ?? ""), Indent = (double)Amount(indent.Text ?? "") });
        }
        panel.Children.Add(new TextBlock { Text = "Поля строки: шрифт, размер, жирный, курсив, выравнивание, интервал до/после (пт), межстрочный, отступ (пт), цвет (#RRGGBB).", TextWrapping = TextWrapping.Wrap, FontSize = 11 }); var error = new TextBlock { Foreground = Brushes.DarkRed }; panel.Children.Add(error); var apply = new Button { Content = "Применить к документу" };
        apply.Click += (_, _) => { try { var st = styles.Select(f => f()).ToArray(); double m = (double)Amount(margin.Text ?? ""); if (st.Any(s => !System.Text.RegularExpressions.Regex.IsMatch(s.Color, "^#[0-9a-fA-F]{6}$"))) throw new ArgumentException("Цвет задаётся в формате #000000."); if (m < 5 || m > 40 || st.Any(s => s.Size < 8 || s.Size > 24 || s.LineSpacing < 1 || s.LineSpacing > 2 || s.Before < 0 || s.After < 0 || s.Indent < 0 || s.Indent > 60)) throw new ArgumentException("Размер шрифта 8–24, поля 5–40 мм, межстрочный 1–2, отступ 0–60 пт."); file = file with { Project = file.Project with { Document = options with { ApprovalText = approvalText.Text ?? "", ShowApproval = approval.IsChecked == true, Landscape = landscape.IsChecked == true, MarginMm = m, Title = st[0], Body = st[1], Table = st[2], Approval = st[3], Summary = st[4], Signature = st[5] } } }; dirty = true; Render(); dialog.Close(); } catch (Exception ex) { error.Text = ex.Message; } }; panel.Children.Add(apply); dialog.Content = Scroll(panel); await dialog.ShowDialog(this);
    }
    static string N(double v) => v.ToString(CultureInfo.InvariantCulture);
    async Task Help() { var answer = await Ask("LexPercent Pro · справка", "1. Создайте проект и заполните реквизиты.\n2. Сформируйте график начислений.\n3. Добавьте ставки и оплаты на вкладке «События».\n4. Рассчитайте и проверьте интервалы.\n5. Сохраните проект и экспортируйте справку.\n\nИстория сохраняется внутри .lpp. Автосохранение создаёт отдельный черновик для восстановления.\n\nТЗ 1.1 · Расчёт выполняется локально, без Интернета.", "Тест расчёта", "Закрыть"); if (answer == "Тест расчёта") await Ask("Тест расчёта", ReferenceExample.Check(), "Закрыть"); }
}
