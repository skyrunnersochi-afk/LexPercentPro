using LexPercent.Domain;
using System.Globalization;
namespace LexPercent.Core;

/// <summary>Deterministic event-driven calculation using agreed specification 1.1.</summary>
public sealed class Calculator
{
    public CalculationResult Calculate(CalculationProject p)
    {
        Validate(p);
        var dated = p.Events.Where(e => !p.FixedRate || e.Kind != EventKind.Rate).Select(e => (Event: e, Effective: e.Kind == EventKind.Payment ? e.Date.AddDays(1) : e.Date))
            .Where(x => x.Effective <= p.End).OrderBy(x => x.Effective).ThenBy(x => x.Event.Kind).ThenBy(x => x.Event.Id).ToArray();
        var groups = dated.GroupBy(x => x.Effective).ToDictionary(g => g.Key, g => g.ToArray());
        var boundary = new SortedSet<DateOnly>(groups.Keys.Where(d => d >= p.Start)) { p.Start, p.End.AddDays(1) };
        for (int y = p.Start.Year + 1; y <= p.End.Year; y++) boundary.Add(new(y, 1, 1));
        decimal balance = 0, rate = p.FixedRate ? p.FixedRatePercent : 0;
        bool rateKnown = p.FixedRate;
        var ledger = new List<LedgerEntry>();
        void Apply((CalculationEvent Event, DateOnly Effective) x)
        {
            var e = x.Event;
            if (e.Kind == EventKind.Rate) { if (!p.FixedRate) { rate = e.Value; rateKnown = true; } }
            else balance += e.Kind == EventKind.Accrual ? e.Value : -e.Value;
            ledger.Add(new(e.Date, x.Effective, e.KindText, e.Value, Math.Max(0, balance), Math.Max(0, -balance)));
        }
        foreach (var x in dated.Where(x => x.Effective < p.Start)) Apply(x);
        var points = boundary.ToArray(); var rows = new List<Interval>();
        for (int i = 0; i < points.Length - 1; i++)
        {
            var a = points[i]; if (groups.TryGetValue(a, out var ev)) foreach (var x in ev) Apply(x);
            if (balance <= 0) continue;
            if (!rateKnown) throw new ArgumentException($"Не указана ставка на {a:dd.MM.yyyy}. Добавьте ставку на эту или более раннюю дату.");
            var z = points[i + 1].AddDays(-1); int n = z.DayNumber - a.DayNumber + 1, yd = DateTime.IsLeapYear(a.Year) ? 366 : 365;
            decimal interest = decimal.Round(balance * rate / 100 / yd * n, 2, MidpointRounding.AwayFromZero);
            string f = string.Format(CultureInfo.GetCultureInfo("ru-RU"), "{0:N2} × {1:N2}% / {2} × {3} = {4:N2} руб.", balance, rate, yd, n, interest);
            rows.Add(new(a, z, n, balance, rate, yd, interest, f));
        }
        // Calendar-year analysis counts only same-year accruals and payments as prescribed in 3.16.
        var years = p.Events.Where(e => e.Date <= p.End && e.Kind != EventKind.Rate).GroupBy(e => e.Date.Year).OrderBy(g => g.Key)
            .Select(g => { var a = g.Where(e => e.Kind == EventKind.Accrual).Sum(e => e.Value); var pay = g.Where(e => e.Kind == EventKind.Payment).Sum(e => e.Value); return new YearAnalysis(g.Key, a, pay, a == 0 ? 0 : decimal.Round(Math.Min(a, pay) / a * 100, 2, MidpointRounding.AwayFromZero)); }).ToArray();
        return new(rows.ToArray(), ledger.ToArray(), years, rows.Sum(r => r.Interest), rows.Sum(r => r.Days), Math.Max(0, balance), Math.Max(0, -balance),
            dated.Where(x => x.Event.Kind == EventKind.Accrual).Sum(x => x.Event.Value), dated.Where(x => x.Event.Kind == EventKind.Payment).Sum(x => x.Event.Value));
    }
    public static void Validate(CalculationProject p)
    {
        if (p.End < p.Start) throw new ArgumentException("Дата окончания расчета раньше даты начала.");
        if (p.End == DateOnly.MaxValue) throw new ArgumentException("Дата окончания должна быть раньше 31.12.9999.");
        if (p.Events.Select(e => e.Id).Distinct().Count() != p.Events.Count) throw new ArgumentException("Обнаружены повторяющиеся идентификаторы событий.");
        foreach (var e in p.Events)
        {
            if (!Enum.IsDefined(e.Kind)) throw new ArgumentException("Неизвестный вид события.");
            if (e.Value < 0 || (e.Kind != EventKind.Rate && e.Value == 0)) throw new ArgumentException("Суммы должны быть положительными, ставка — неотрицательной.");
            if (e.Kind != EventKind.Rate && decimal.Round(e.Value, 2) != e.Value) throw new ArgumentException("Сумма события должна содержать не более двух знаков после запятой.");
            if (e.Kind == EventKind.Payment && e.Date == DateOnly.MaxValue) throw new ArgumentException("Дата оплаты должна быть раньше 31.12.9999.");
        }
        if (p.Events.Where(e => e.Kind == EventKind.Rate).GroupBy(e => e.Date).Any(g => g.Count() > 1)) throw new ArgumentException("На одну дату указано несколько ставок. Оставьте одну запись.");
        if (p.FixedRatePercent < 0) throw new ArgumentException("Фиксированная ставка не может быть отрицательной.");
    }
    /// <summary>Replace generated accruals only; manually changed or entered events remain.</summary>
    public static CalculationProject GenerateSchedule(CalculationProject p)
    {
        if (p.ScheduleEnd < p.Start) throw new ArgumentException("Конец графика раньше первого начисления.");
        if (p.MonthlyDay is < 1 or > 31 || p.JanuaryDay is < 1 or > 31) throw new ArgumentException("Число месяца должно быть от 1 до 31.");
        if (p.FirstAmount <= 0 || p.MonthlyAmount <= 0) throw new ArgumentException("Укажите положительные суммы начислений.");
        var events = p.Events.Where(e => !(e.Kind == EventKind.Accrual && e.Automatic)).ToList();
        void Add(DateOnly d, decimal v) { if (!events.Any(e => e.Kind == EventKind.Accrual && e.Date == d)) events.Add(new(Guid.NewGuid(), d, EventKind.Accrual, v, "По графику", true)); }
        Add(p.Start, p.FirstAmount);
        var month = new DateOnly(p.Start.Year, p.Start.Month, 1);
        while (month.Year < 9999 || month.Month < 12)
        {
            month = month.AddMonths(1); if (month > p.ScheduleEnd) break;
            int day = month.Month == 1 ? p.JanuaryDay : p.MonthlyDay;
            if (day > DateTime.DaysInMonth(month.Year, month.Month)) throw new ArgumentException($"В {month:MM.yyyy} нет {day} числа. Укажите существующую дату или добавьте начисление вручную.");
            var d = new DateOnly(month.Year, month.Month, day); if (d <= p.ScheduleEnd) Add(d, p.MonthlyAmount);
        }
        return p with { Events = events.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList() };
    }
}
