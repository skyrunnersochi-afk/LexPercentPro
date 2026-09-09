using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LexPercent.Core;
using LexPercent.Domain;

namespace LexPercent.UI;

public sealed partial class MainWindow
{
    Control RatesView()
    {
        var panel = new Grid { RowDefinitions = new("Auto,Auto,Auto,*,Auto") };
        panel.Children.Add(TitleBlock("Ключевая ставка Центрального банка России", "Введите процент годовых и дату, с которой он действует. Каждое изменение — отдельная строка."));
        var mode = new TextBlock
        {
            Text = file.Project.FixedRate
                ? "Сейчас выбрана фиксированная ставка. Нажмите «Применить ключевую ставку», чтобы использовать эту таблицу."
                : "Расчёт использует ключевую ставку по датам из этой таблицы.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new(0, 0, 0, 12),
            Foreground = Teal
        };
        Grid.SetRow(mode, 1); panel.Children.Add(mode);
        var entry = new WrapPanel { Margin = new(0, 0, 0, 12) };
        TextBox Input(string caption, string text, double width)
        {
            var box = new TextBox { Text = text, Width = width };
            var group = new StackPanel { Spacing = 5, Margin = new(0, 0, 12, 8) };
            group.Children.Add(new TextBlock { Text = caption, Foreground = Muted });
            group.Children.Add(box); entry.Children.Add(group); return box;
        }
        var date = Input("Действует с (ДД.ММ.ГГГГ)", file.Project.Start.ToString("dd.MM.yyyy"), 210);
        var percent = Input("Ключевая ставка, % годовых", "", 240);
        percent.PlaceholderText = "Например: 14,25";
        var comment = Input("Комментарий / источник", "", 270);
        Grid.SetRow(entry, 2); panel.Children.Add(entry);
        var grid = GridFor(file.Project.Events.Where(e => e.Kind == EventKind.Rate).OrderBy(e => e.Date).ToArray(),
            ("Действует с", "DateText", 1), ("Ставка, % годовых", "ValueText", 1), ("Комментарий", "Comment", 2));
        Guid? selected = null;
        grid.SelectionChanged += (_, _) =>
        {
            if (grid.SelectedItem is not CalculationEvent e) return;
            selected = e.Id; date.Text = e.DateText; percent.Text = e.ValueText; comment.Text = e.Comment;
        };
        Grid.SetRow(grid, 3); panel.Children.Add(grid);
        var bottom = new StackPanel { Spacing = 8, Margin = new(0, 12, 0, 0) };
        bottom.Children.Add(new TextBlock { Text = "Первая ставка должна действовать не позднее первого дня задолженности. Она применяется до следующего изменения. Можно ввести значения вручную или применить официальный справочник ЦБ.", TextWrapping = TextWrapping.Wrap, Foreground = Muted });
        var online = new WrapPanel();
        online.Children.Add(Button("Обновить ключевую ставку ЦБ РФ", () => UpdateCbr(true)));
        online.Children.Add(Button("Все ставки по периодам", () => Navigate("История ставок ЦБ РФ")));
        online.Children.Add(Button("Применить ставки ЦБ к расчёту", () => { ApplyCbrToProject(); return Task.CompletedTask; }));
        bottom.Children.Add(online);
        var buttons = new WrapPanel();
        Task Put(bool update)
        {
            if (update && selected is null) throw new ArgumentException("Выберите строку ставки для изменения.");
            var value = new CalculationEvent(update ? selected!.Value : Guid.NewGuid(), Date(date.Text ?? ""), EventKind.Rate, Amount(percent.Text ?? ""), comment.Text ?? "");
            var project = file.Project with { FixedRate = false, Events = file.Project.Events.Where(e => !update || e.Id != value.Id).Append(value).ToList() };
            Calculator.Validate(project);
            file = file with { Project = project, Result = null }; dirty = true;
            status.Text = $"Ключевая ставка {value.ValueText}% с {value.DateText} сохранена в проекте"; Render(); return Task.CompletedTask;
        }
        buttons.Children.Add(Button("Добавить ставку", () => Put(false)));
        buttons.Children.Add(Button("Изменить выбранную", () => Put(true)));
        buttons.Children.Add(Button("Удалить выбранную", async () =>
        {
            if (selected is null) return;
            if (await Ask("Удалить ставку", "Удалить выбранное значение ключевой ставки?", "Удалить", "Отмена") != "Удалить") return;
            file = file with { Project = file.Project with { Events = file.Project.Events.Where(e => e.Id != selected).ToList() }, Result = null }; dirty = true; Render();
        }));
        buttons.Children.Add(Button("Применить ключевую ставку", () =>
        {
            file = file with { Project = file.Project with { FixedRate = false }, Result = null }; dirty = true; Render(); return Task.CompletedTask;
        }));
        buttons.Children.Add(Button("Рассчитать", async () => { file = file with { Project = file.Project with { FixedRate = false }, Result = null }; dirty = true; await Navigate("Результат"); }));
        bottom.Children.Add(buttons); Grid.SetRow(bottom, 4); panel.Children.Add(bottom); return panel;
    }
}
