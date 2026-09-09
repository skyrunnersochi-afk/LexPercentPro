using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LexPercent.Domain;
using LexPercent.Storage;

namespace LexPercent.UI;

public sealed partial class MainWindow
{
    Control SignatorySelection()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new(0, 0, 0, 16) };
        panel.Children.Add(TitleBlock("Подписанты справки", "Отметьте подписи для печати. Они выводятся в порядке выбора и сохраняются вместе с расчётом."));
        var people = file.Project.Signatories.Concat(settings.Signatories).DistinctBy(s => s.Id).ToArray();
        foreach (var person in people)
        {
            var check = new CheckBox { Content = new TextBlock { Text = person + "\n" + person.Rank, TextWrapping = TextWrapping.Wrap }, IsChecked = file.Project.Signatories.Any(s => s.Id == person.Id) };
            check.IsCheckedChanged += (_, _) =>
            {
                var selected = file.Project.Signatories.Where(s => s.Id != person.Id).ToList();
                if (check.IsChecked == true) selected.Add(person with { });
                file = file with { Project = file.Project with { Signatories = selected } }; Changed();
            };
            panel.Children.Add(check);
        }
        panel.Children.Add(Button("Открыть справочник подписантов", async () => { Commit(); await EditSignatories(this); Render(); }));
        return panel;
    }
    async Task EditSignatories(Window owner)
    {
        var dialog = new Window { Title = "Справочник подписантов", Width = 720, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new(22), Spacing = 9 };
        panel.Children.Add(new TextBlock { Text = "Добавление и изменение подписантов", FontSize = 20 });
        var list = new ListBox { ItemsSource = settings.Signatories.ToArray(), Height = 130 }; panel.Children.Add(list);
        TextBox Input(string title, bool multi = false)
        {
            panel.Children.Add(new TextBlock { Text = title });
            var input = new TextBox { AcceptsReturn = multi, MinHeight = multi ? 70 : 34, TextWrapping = TextWrapping.Wrap }; panel.Children.Add(input); return input;
        }
        var position = Input("Должность и подразделение (с нужными переносами строк)", true);
        var rank = Input("Специальное звание"); var name = Input("Инициалы и фамилия (например, А.И. Мороз)");
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is Signatory s) { position.Text = s.Position; rank.Text = s.Rank; name.Text = s.Name; } };
        var error = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        var buttons = new WrapPanel();
        void Action(string title, Action action)
        {
            var b = new Button { Content = title, Margin = new(0, 0, 8, 6) };
            b.Click += (_, _) => { try { action(); error.Text = ""; } catch (Exception ex) { error.Text = ex.Message; } }; buttons.Children.Add(b);
        }
        void Save(bool update)
        {
            if (update && list.SelectedItem is not Signatory) throw new ArgumentException("Выберите подписанта для изменения.");
            if (string.IsNullOrWhiteSpace(position.Text) || string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Заполните должность и фамилию.");
            var id = update ? ((Signatory)list.SelectedItem!).Id : Guid.NewGuid();
            var person = new Signatory(id, position.Text.Trim().Replace("\r\n", "\n"), rank.Text?.Trim() ?? "", name.Text.Trim());
            var next = settings with { Signatories = settings.Signatories.Where(s => s.Id != id).Append(person).ToList() };
            SettingsStore.Save(next); settings = next; list.ItemsSource = settings.Signatories.ToArray(); list.SelectedItem = person;
        }
        Action("Добавить", () => Save(false)); Action("Сохранить изменения", () => Save(true));
        Action("Удалить из справочника", () =>
        {
            if (list.SelectedItem is not Signatory person) return;
            var next = settings with { Signatories = settings.Signatories.Where(s => s.Id != person.Id).ToList() };
            SettingsStore.Save(next); settings = next; list.ItemsSource = settings.Signatories.ToArray();
        });
        panel.Children.Add(buttons);
        panel.Children.Add(new TextBlock { Text = "Изменения справочника не изменяют подписи в ранее сохранённых расчётах. Для нового расчёта выберите нужных подписантов в карточке проекта.", TextWrapping = TextWrapping.Wrap, Foreground = Muted });
        var close = new Button { Content = "Готово" }; close.Click += (_, _) => dialog.Close(); panel.Children.Add(close);
        dialog.Content = Scroll(panel); await dialog.ShowDialog(owner);
    }
}
