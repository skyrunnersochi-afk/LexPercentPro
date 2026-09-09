namespace LexPercent.Domain;

/// <summary>Copied into each project so directory edits do not change saved signatures.</summary>
public sealed record Signatory(Guid Id, string Position, string Rank, string Name)
{
    public static Signatory Moroz { get; } = new(new Guid("fd3ac011-23af-4976-b5d4-07e738190821"),
        "Заместитель начальника ОУиО\nуправления финансового обеспечения", "подполковник полиции", "А.И. Мороз");
    public override string ToString() => $"{Name} — {Position.Replace('\n', ' ')}";
}
