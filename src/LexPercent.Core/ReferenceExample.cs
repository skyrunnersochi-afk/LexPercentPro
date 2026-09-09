using LexPercent.Domain;
namespace LexPercent.Core;
/// <summary>Approved regression fixture; supplied rates are fixed test data, not a live rate feed.</summary>
public static class ReferenceExample
{
    public static CalculationProject Create()
    {
        var p = Calculator.GenerateSchedule(new() { Debtor = "Иванов Иван Иванович", Number = "1", CalculationDate = new(2026, 9, 8), Basis = "по соглашению о возмещении затрат на обучение (уволен 11.10.2024 г.)", Executor = "Начальник ФЭО\nполковник полиции                         С.В. Маркова" });
        foreach (var (d, v) in new[] { ("2024-10-11", 19m), ("2024-10-28", 21m), ("2025-06-09", 20m), ("2025-07-28", 18m), ("2025-09-15", 17m), ("2025-10-27", 16.5m), ("2025-12-22", 16m), ("2026-02-16", 15.5m), ("2026-03-23", 15m), ("2026-04-27", 14.5m), ("2026-06-22", 14.25m), ("2026-07-27", 14m) }) p.Events.Add(new(Guid.NewGuid(), DateOnly.ParseExact(d, "yyyy-MM-dd"), EventKind.Rate, v, "Согласованный эталон"));
        foreach (var (d, v) in new[] { ("2024-11-12", 6100m), ("2024-11-15", 6000m), ("2025-01-17", 6050m), ("2025-02-19", 6050m), ("2025-10-17", 6000m) }) p.Events.Add(new(Guid.NewGuid(), DateOnly.ParseExact(d, "yyyy-MM-dd"), EventKind.Payment, v));
        return p;
    }
    public static string Check()
    {
        var r = new Calculator().Calculate(Create());
        if (r.Interest != 14393.74m || r.Days != 673 || r.Principal != 96607.59m || r.Intervals.Length != 38) throw new InvalidOperationException("Эталонный расчет не совпал с ТЗ.");
        return "Эталон Иванова: ПРОЙДЕН\n38 интервалов · 673 дня\nПроценты: 14 393,74 руб.\nОсновной долг: 96 607,59 руб.";
    }
}
