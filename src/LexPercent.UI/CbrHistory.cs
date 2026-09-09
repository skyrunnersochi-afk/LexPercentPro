using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LexPercent.Domain;
using LexPercent.Storage;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace LexPercent.UI;

public sealed partial class MainWindow
{
    readonly CbrRates cbr = new(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, Path.Combine(SettingsStore.DirectoryPath, "cbr-rates.json"));
    CbrSnapshot? rateSnapshot;
    bool updatingRates;
    DateTime lastRateAttempt = DateTime.MinValue;
    bool networkWasAvailable;
    readonly DispatcherTimer rateTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    void StartRateUpdates()
    {
        rateSnapshot = cbr.Load();
        async Task Check()
        {
            bool online = NetworkInterface.GetIsNetworkAvailable();
            bool reconnected = online && !networkWasAvailable; networkWasAvailable = online;
            if (online && !busy && !updatingRates && (reconnected || DateTime.Now - lastRateAttempt > (rateSnapshot is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(6))))
                await UpdateCbr(false);
        }
        rateTimer.Tick += async (_, _) => await Check();
        Closed += (_, _) => rateTimer.Stop();
        rateTimer.Start(); _ = Check();
    }
    async Task UpdateCbr(bool manual)
    {
        if (updatingRates) { status.Text = "Обновление ставок ЦБ уже выполняется…"; return; }
        updatingRates = true; lastRateAttempt = DateTime.Now;
        status.Text = "Загрузка истории ключевой ставки с cbr.ru…";
        try
        {
            rateSnapshot = await cbr.Update();
            status.Text = $"ЦБ РФ: загружено {rateSnapshot.Rates.Length} периодов; данные по {rateSnapshot.Through:dd.MM.yyyy}.";
            if (!file.Project.FixedRate && !file.Project.Events.Any(e => e.Kind == EventKind.Rate && e.Comment != CbrRates.Source)) file = file with { Result = null };
            if (page == "История ставок ЦБ РФ" || manual && page == "Ключевая ставка ЦБ") Render();
        }
        catch (Exception e)
        {
            status.Text = "Ставки ЦБ не обновлены; сохранённые данные доступны. " + e.Message;
            if (manual) await Ask("Обновление ключевой ставки", "Не удалось получить данные с сайта ЦБ РФ. Проверьте подключение и повторите обновление. Сохранённые ставки не удалены.\n\n" + e.Message, "Закрыть");
        }
        finally { updatingRates = false; }
    }
    void ApplyCbrToProject()
    {
        if (rateSnapshot is null) throw new ArgumentException("Сначала нажмите «Обновить ключевую ставку ЦБ РФ». Интернет нужен только для загрузки справочника.");
        Commit();
        file = file with { Project = CbrRates.Apply(file.Project, rateSnapshot), Result = null }; dirty = true;
        status.Text = "Ставки официального справочника применены к проекту";
        Render();
    }
    void UseAvailableCbrRates()
    {
        if (file.Project.FixedRate || rateSnapshot is null) return;
        var existing = file.Project.Events.Where(e => e.Kind == EventKind.Rate).ToArray();
        if (existing.Any(e => e.Comment != CbrRates.Source)) return;
        var updated = CbrRates.Apply(file.Project, rateSnapshot);
        if (!updated.Events.SequenceEqual(file.Project.Events)) { file = file with { Project = updated, Result = null }; dirty = true; }
    }
    public sealed record RatePeriod(string Start, string End, string Percent);
    Control CbrHistoryView()
    {
        var panel = new Grid { RowDefinitions = new("Auto,Auto,Auto,*") };
        panel.Children.Add(TitleBlock("История ключевых ставок ЦБ РФ", "Официальный справочник с начала публикации ключевой ставки. Данные хранятся на этом компьютере."));
        var note = new TextBlock { Text = rateSnapshot is null ? "Справочник пока не загружен. Подключитесь к Интернету и нажмите «Обновить»." : $"Обновлено: {rateSnapshot.Updated.LocalDateTime:dd.MM.yyyy HH:mm}. Данные ЦБ по {rateSnapshot.Through:dd.MM.yyyy}. Последняя известная ставка: {rateSnapshot.Rates[^1].Percent:N2}%.", TextWrapping = TextWrapping.Wrap, Foreground = Muted, Margin = new(0, 0, 0, 12) };
        Grid.SetRow(note, 1); panel.Children.Add(note);
        var actions = new WrapPanel();
        actions.Children.Add(Button("Обновить ключевую ставку ЦБ РФ", () => UpdateCbr(true)));
        actions.Children.Add(Button("Применить к текущему расчёту", () => { ApplyCbrToProject(); return Task.CompletedTask; }));
        Grid.SetRow(actions, 2); panel.Children.Add(actions);
        var rates = rateSnapshot?.Rates ?? [];
        var rows = rates.Select((r, i) => new RatePeriod(r.Start.ToString("dd.MM.yyyy"), i + 1 < rates.Length ? rates[i + 1].Start.AddDays(-1).ToString("dd.MM.yyyy") : "Последняя известная", r.Percent.ToString("N2"))).Reverse();
        var grid = GridFor(rows, ("Начало действия", "Start", 1), ("Окончание действия", "End", 1.5), ("Ставка, % годовых", "Percent", 1));
        Grid.SetRow(grid, 3); panel.Children.Add(grid); return panel;
    }
}
