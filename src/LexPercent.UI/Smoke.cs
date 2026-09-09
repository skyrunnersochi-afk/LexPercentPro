using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using LexPercent.Domain;
using Avalonia;
using Avalonia.Media.Imaging;
using LexPercent.Core;
using LexPercent.Documents;

namespace LexPercent.UI;

public sealed partial class MainWindow
{
    async Task Smoke(string folder)
    {
        Directory.CreateDirectory(folder);
        await CheckRateEntry();
        if (App.Arguments.Contains("--online")) { rateSnapshot = await cbr.Update(); if (rateSnapshot.Rates.Length < 20) throw new InvalidOperationException("Incomplete official history"); }

        fields.Clear(); file = new() { Project = ReferenceExample.Create() with { Signatories = [Signatory.Moroz], Basis = "Справка от 09.09.2026" } };
        await Calculate();
        foreach (var target in new[] { "Общие сведения", "Ключевая ставка ЦБ", "История ставок ЦБ РФ", "События", "Результат", "Документ", "История" })
        {
            await Navigate(target); await Task.Delay(250);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
            bitmap.Render(this); bitmap.Save(Path.Combine(folder, target + ".png"), PngBitmapEncoderOptions.Default);
        }
        foreach (var protocol in new[] { false, true })
            foreach (var ext in new[] { "pdf", "docx", "xlsx" })
                Exporter.Export(Path.Combine(folder, (protocol ? "protocol" : "reference") + "." + ext), Report.Create(file.Project, file.Result!, protocol));
        file = await store.SaveAsync(Path.Combine(folder, "Иванов — эталон.lpp"), file);
        var loaded = await store.LoadAsync(Path.Combine(folder, "Иванов — эталон.lpp"));
        if (loaded.Result?.Interest != 14393.74m) throw new InvalidOperationException("Ошибка сохранения эталона");
        File.WriteAllText(Path.Combine(folder, "smoke.txt"), ReferenceExample.Check() + "\nВвод и изменение ключевой ставки через интерфейс; семь экранов, PDF preview, шесть экспортов и открытие .lpp: успешно.");
    }
    async Task CheckRateEntry()
    {
        fields.Clear(); file = new() { Project = new() { Start = new(2025, 1, 1), End = new(2025, 1, 10), FixedRate = true, FixedRatePercent = 99, Events = [new(Guid.NewGuid(), new(2025, 1, 1), EventKind.Accrual, 1000)] } };
        await Navigate("Ключевая ставка ЦБ");
        async Task Click(string text)
        {
            var button = body.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == text);
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            for (int i = 0; busy && i < 100; i++) await Task.Delay(10);
            if (busy) throw new InvalidOperationException("Rate entry action did not complete: " + text);
        }
        void Input(string date, string value)
        {
            var inputs = body.GetLogicalDescendants().OfType<TextBox>().ToArray();
            inputs[0].Text = date; inputs[1].Text = value;
        }
        Input("01.01.2025", "36,5"); await Click("Добавить ставку");
        Input("06.01.2025", "73"); await Click("Добавить ставку");
        if (file.Project.FixedRate || file.Project.Events.Count(e => e.Kind == EventKind.Rate) != 2) throw new InvalidOperationException("Key rate entries were not applied");
        await Calculate();
        if (file.Result!.Interest != 15m) throw new InvalidOperationException("Two key rates must produce 15 rubles");
        var grid = body.GetLogicalDescendants().OfType<DataGrid>().Single();
        grid.SelectedItem = file.Project.Events.Single(e => e.Kind == EventKind.Rate && e.Date == new DateOnly(2025, 1, 6));
        Input("06.01.2025", "36,5"); await Click("Изменить выбранную");
        await Calculate();
        if (file.Result!.Interest != 10m) throw new InvalidOperationException("Edited key rate must produce 10 rubles");
    }

}
