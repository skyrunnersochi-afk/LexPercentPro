using System.Globalization;
namespace LexPercent.Domain;

/// <summary>Stored event date is the actual date; payment effectiveness is determined by Core.</summary>
public enum EventKind { Rate, Accrual, Payment }
public sealed record CalculationEvent(Guid Id, DateOnly Date, EventKind Kind, decimal Value, string Comment = "", bool Automatic = false)
{
    public string DateText => Date.ToString("dd.MM.yyyy");
    public string OriginText => Automatic ? "По графику" : "Вручную";
    public string KindText => Kind switch { EventKind.Rate => "Ставка", EventKind.Accrual => "Начисление", _ => "Оплата" };
    public string ValueText => Value.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
}
public sealed record BlockStyle
{
    public string Font { get; init; } = "Times New Roman";
    public double Size { get; init; } = 11;
    public string Color { get; init; } = "#000000";
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public string Align { get; init; } = "left";
    public double Before { get; init; } = 4;
    public double After { get; init; } = 4;
    public double LineSpacing { get; init; } = 1.15;
    public double Indent { get; init; }
}
public sealed record DocumentOptions
{
    public const string DefaultApprovalText = "УТВЕРЖДАЮ\nНачальник Краснодарского\nуниверситета МВД России\nгенерал-майор полиции\n_____________ Павленков Р.В.\n\"____\" _______________ 20___ г.";
    public string ApprovalText { get; init; } = DefaultApprovalText;
    public bool ShowApproval { get; init; } = true;
    public bool Landscape { get; init; }
    public double MarginMm { get; init; } = 18;
    public BlockStyle Title { get; init; } = new() { Size = 14, Bold = true, Align = "center" };
    public BlockStyle Body { get; init; } = new();
    public BlockStyle Table { get; init; } = new() { Size = 10 };
    public BlockStyle Approval { get; init; } = new() { Align = "right" };
    public BlockStyle Summary { get; init; } = new() { Bold = true };
    public BlockStyle Signature { get; init; } = new();
}
/// <summary>Serializable input snapshot. All events share one list.</summary>
public sealed record CalculationProject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Number { get; init; } = "1";
    public DateOnly CalculationDate { get; init; } = DateOnly.FromDateTime(DateTime.Today);
    public string Debtor { get; init; } = "";
    public string Organization { get; init; } = "";
    public string Manager { get; init; } = "";
    public string Executor { get; init; } = "";
    public List<Signatory> Signatories { get; init; } = [];
    public string Basis { get; init; } = "";
    public DateOnly Start { get; init; } = new(2024, 10, 11);
    public DateOnly End { get; init; } = new(2026, 9, 8);
    public DateOnly ScheduleEnd { get; init; } = new(2026, 6, 11);
    public int MonthlyDay { get; init; } = 11;
    public int JanuaryDay { get; init; } = 16;
    public decimal FirstAmount { get; init; } = 6038.59m;
    public decimal MonthlyAmount { get; init; } = 6038.45m;
    public bool FixedRate { get; init; }
    public decimal FixedRatePercent { get; init; } = 14;
    public List<CalculationEvent> Events { get; init; } = [];
    public DocumentOptions Document { get; init; } = new();
}
public sealed record Interval(DateOnly Start, DateOnly End, int Days, decimal Debt, decimal Rate, int YearDays, decimal Interest, string Formula)
{
    public string Period => $"{Start:dd.MM.yyyy}–{End:dd.MM.yyyy}";
    public string DebtText => Debt.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
    public string RateText => Rate.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + "%";
    public string InterestText => Interest.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
}
public sealed record LedgerEntry(DateOnly ActualDate, DateOnly EffectiveDate, string Event, decimal Value, decimal Debt, decimal Advance);
public sealed record YearAnalysis(int Year, decimal Accruals, decimal Payments, decimal Percent);
public sealed record CalculationResult(Interval[] Intervals, LedgerEntry[] Ledger, YearAnalysis[] Years, decimal Interest, int Days, decimal Principal, decimal Advance, decimal Accruals, decimal Payments);
public sealed record ProjectVersion(int Number, DateTimeOffset SavedAt, string Description, CalculationProject Project, CalculationResult? Result);
public sealed record ProjectFile
{
    public int FormatVersion { get; init; } = 1;
    public CalculationProject Project { get; init; } = new();
    public CalculationResult? Result { get; init; }
    public List<ProjectVersion> Versions { get; init; } = [];
}
