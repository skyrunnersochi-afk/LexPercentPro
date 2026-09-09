using LexPercent.Domain;
using System.Globalization;
namespace LexPercent.Documents;

public sealed record ReportRow(string[] Cells, object[]? Values = null, bool Header = false);
public sealed record ReportBlock(string Text, BlockStyle Style, string? RightText = null);
/// <summary>One content model consumed by all document formats; no interest calculations.</summary>
public sealed record Report(ReportBlock[] Before, ReportRow[] Rows, ReportBlock[] After, DocumentOptions Options)
{
    static string Money(decimal v) => v.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
    public static Report Create(CalculationProject p, CalculationResult r, bool protocol = false)
    {
        var o = p.Document; var before = new List<ReportBlock>(); var after = new List<ReportBlock>();
        if (o.ShowApproval && !protocol) before.Add(new(o.ApprovalText, o.Approval));
        before.Add(new(protocol ? "ПРОТОКОЛ РАСЧЕТА" : "СПРАВКА", o.Title));
        before.Add(new($"№ {p.Number} от {p.CalculationDate:dd.MM.yyyy}", o.Body with { Align = "center" }));
        before.Add(new("Расчет процентов за пользование чужими денежными средствами", o.Body with { Align = "center" }));
        var basis = p.Basis.Trim();
        if (basis.Length > 0 && !basis.StartsWith("основание:", StringComparison.OrdinalIgnoreCase)) basis = "Основание: " + basis;
        before.Add(new($"{p.Debtor}\n{basis}\nза период с {p.Start:dd.MM.yyyy} по {p.End:dd.MM.yyyy}", o.Body));
        var rows = new List<ReportRow> { new(["Период", "Задолженность, руб.", "Ставка, %", "Дней", "Проценты, руб."], Header: true) };
        foreach (var x in r.Intervals) rows.Add(new([x.Period, x.DebtText, x.Rate.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")), x.Days.ToString(), x.InterestText], [x.Period, x.Debt, x.Rate, x.Days, x.Interest]));
        rows.Add(new(["ИТОГО", "", "", r.Days.ToString(), Money(r.Interest)], ["ИТОГО", "", "", r.Days, r.Interest]));
        after.Add(new("Формула: П = З × С / 100 / 365 (366) × Д.", o.Body));
        after.Add(new($"Всего дней начисления процентов: {r.Days}.\nСумма процентов: {Money(r.Interest)} руб.", o.Summary));
        if (!protocol) after.Add(new(MoneyWords.Rubles(r.Interest), o.Summary));
        if (protocol)
        {
            before.Add(new("Начисление включается в расчет в указанную дату. Оплата уменьшает расчетный долг со следующего календарного дня. Выходные даты не переносят. Каждый интервал округляется отдельно по AwayFromZero.", o.Body));
            after.Add(new($"Основной долг: {Money(r.Principal)} руб. Переплата: {Money(r.Advance)} руб.\nНачислено: {Money(r.Accruals)} руб. Учтено оплат: {Money(r.Payments)} руб.", o.Summary));
            after.Add(new("СОБЫТИЯ И ИЗМЕНЕНИЕ ДОЛГА", o.Title));
            foreach (var e in r.Ledger) after.Add(new($"{e.ActualDate:dd.MM.yyyy} — {e.Event}: {Money(e.Value)}; действует {e.EffectiveDate:dd.MM.yyyy}. Долг {Money(e.Debt)}, переплата {Money(e.Advance)}.", o.Body));
            after.Add(new("ПОДРОБНЫЕ ВЫЧИСЛЕНИЯ", o.Title));
            foreach (var x in r.Intervals) after.Add(new($"{x.Period}: {x.Formula}", o.Body));
            after.Add(new("АНАЛИЗ ПО КАЛЕНДАРНЫМ ГОДАМ", o.Title));
            after.Add(new("Учитываются начисления и оплаты соответствующего года; остаток предыдущих лет в процент погашения не входит. Процент ограничен размером начислений года.", o.Body));
            foreach (var y in r.Years) after.Add(new($"{y.Year}: начислено {Money(y.Accruals)}; оплачено в этом году {Money(y.Payments)}; погашение {Money(y.Percent)}%.", o.Body));
        }
        else if (p.Signatories.Count > 0)
        {
            foreach (var signer in p.Signatories)
                after.Add(new(signer.Position.TrimEnd() + "\n" + signer.Rank, o.Signature with { Align = "left" }, signer.Name));
        }
        else after.Add(new(p.Executor, o.Signature));
        return new(before.ToArray(), rows.ToArray(), after.ToArray(), o);
    }
    public string PlainText() => string.Join("\n\n", Before.Select(x => x.Text)) + "\n\n" + string.Join("\n", Rows.Select(r => string.Join("    ", r.Cells))) + "\n\n" + string.Join("\n\n", After.Select(x => x.Text + (x.RightText is null ? "" : "    " + x.RightText)));
}
