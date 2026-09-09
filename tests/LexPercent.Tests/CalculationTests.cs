using LexPercent.Core;
using LexPercent.Domain;
using LexPercent.Storage;
namespace LexPercent.Tests;

public class CalculationTests
{
    [Fact]
    public void FixedRateIgnoresHistoricRateBoundaries()
    {
        var p = P(E(new(2025, 1, 1), EventKind.Accrual, 5));
        var a = new Calculator().Calculate(p);
        var b = new Calculator().Calculate(p with { Events = [.. p.Events, E(new(2025, 1, 2), EventKind.Rate, 10)] });
        Assert.Equal(a.Intervals, b.Intervals);
        Assert.Equal(.05m, b.Interest);
    }
    static CalculationEvent E(DateOnly date, EventKind kind, decimal value) => new(Guid.NewGuid(), date, kind, value);
    static CalculationProject P(params CalculationEvent[] events) => new() { Start = new(2025, 1, 1), End = new(2025, 1, 10), FixedRate = true, FixedRatePercent = 36.5m, Events = events.ToList() };
    [Fact] public void ApprovedReferenceMatches() { var r = new Calculator().Calculate(ReferenceExample.Create()); Assert.Equal(14393.74m, r.Interest); Assert.Equal(673, r.Days); Assert.Equal(96607.59m, r.Principal); Assert.Equal(38, r.Intervals.Length); Assert.Equal(126807.59m, r.Accruals); Assert.Equal(30200m, r.Payments); Assert.DoesNotContain(r.Intervals, x => x.Start <= new DateOnly(2024, 11, 16) && x.End >= new DateOnly(2024, 11, 16)); }
    [Fact] public void PaymentDayIsIncluded() { var p = P(E(new(2025, 1, 1), EventKind.Accrual, 1000), E(new(2025, 1, 5), EventKind.Payment, 1000)); var r = new Calculator().Calculate(p); Assert.Equal(5, r.Days); Assert.Equal(5m, r.Interest); Assert.Equal(0, r.Principal); }
    [Fact] public void PartialPaymentChangesNextDay() { var r = new Calculator().Calculate(P(E(new(2025, 1, 1), EventKind.Accrual, 1000), E(new(2025, 1, 5), EventKind.Payment, 400))); Assert.Equal(8m, r.Interest); Assert.Equal(600m, r.Principal); }
    [Fact] public void AdvanceCarriesIntoFutureAccrual() { var r = new Calculator().Calculate(P(E(new(2024, 12, 31), EventKind.Payment, 1200), E(new(2025, 1, 1), EventKind.Accrual, 1000), E(new(2025, 1, 8), EventKind.Accrual, 500))); Assert.Equal(3, r.Days); Assert.Equal(.90m, r.Interest); Assert.Equal(300m, r.Principal); }
    [Fact] public void YearBoundaryUses366And365() { var p = P(E(new(2024, 12, 31), EventKind.Accrual, 36600)) with { Start = new(2024, 12, 31), End = new(2025, 1, 1), FixedRatePercent = 10 }; var r = new Calculator().Calculate(p); Assert.Equal(2, r.Intervals.Length); Assert.Equal(20.03m, r.Interest); Assert.Equal(366, r.Intervals[0].YearDays); Assert.Equal(365, r.Intervals[1].YearDays); }
    [Fact] public void RateChangeIsEffectiveSameDay() { var p = P(E(new(2025, 1, 1), EventKind.Accrual, 1000), E(new(2025, 1, 1), EventKind.Rate, 36.5m), E(new(2025, 1, 6), EventKind.Rate, 73m)) with { FixedRate = false }; Assert.Equal(15m, new Calculator().Calculate(p).Interest); }
    [Fact] public void MissingRateAndDuplicateRateAreRejected() { Assert.Throws<ArgumentException>(() => new Calculator().Calculate(P(E(new(2025, 1, 1), EventKind.Accrual, 100)) with { FixedRate = false })); Assert.Throws<ArgumentException>(() => new Calculator().Calculate(P(E(new(2025, 1, 1), EventKind.Rate, 10), E(new(2025, 1, 1), EventKind.Rate, 12)) with { FixedRate = false })); }
    [Fact] public void HalfCentRoundsAwayFromZero() { var p = P(E(new(2025, 1, 1), EventKind.Accrual, 5)) with { End = new(2025, 1, 1) }; Assert.Equal(.01m, new Calculator().Calculate(p).Interest); }
    [Fact] public void NoDebtReturnsNoIntervals() { Assert.Empty(new Calculator().Calculate(P()).Intervals); }
    [Fact] public void ReorderingEventsDoesNotChangeResult() { var p = ReferenceExample.Create(); var c = new Calculator(); var a = c.Calculate(p); p.Events.Reverse(); Assert.Equal(a.Intervals, c.Calculate(p).Intervals); }
    [Fact] public void CultureDoesNotChangeResult() { var old = System.Globalization.CultureInfo.CurrentCulture; try { System.Globalization.CultureInfo.CurrentCulture = new("en-US"); var a = new Calculator().Calculate(ReferenceExample.Create()); System.Globalization.CultureInfo.CurrentCulture = new("ru-RU"); Assert.Equal(a.Intervals, new Calculator().Calculate(ReferenceExample.Create()).Intervals); } finally { System.Globalization.CultureInfo.CurrentCulture = old; } }
    [Fact] public void ScheduleHonorsJanuaryAndLastAccrual() { var p = ReferenceExample.Create(); Assert.Contains(p.Events, e => e.Kind == EventKind.Accrual && e.Date == new DateOnly(2025, 1, 16)); Assert.Equal(21, p.Events.Count(e => e.Kind == EventKind.Accrual)); Assert.DoesNotContain(p.Events, e => e.Kind == EventKind.Accrual && e.Date > new DateOnly(2026, 6, 11)); }
    [Fact]
    public async Task VersionsAndRecoveryRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LexPercent-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir); var path = Path.Combine(dir, "sample.lpp");
        try { var store = new ProjectStore(); var p = ReferenceExample.Create(); var f = await store.SaveAsync(path, new() { Project = p, Result = new Calculator().Calculate(p) }); Assert.Single(f.Versions); f = await store.SaveAsync(path, f); Assert.Single(f.Versions); f = await store.SaveAsync(path, f with { Project = p with { Debtor = "Другой" } }); Assert.Equal(2, f.Versions.Count); f = store.Restore(f, 1); Assert.Equal(3, f.Versions.Count); Assert.Equal(p.Debtor, f.Project.Debtor); await store.SaveAsync(path, f); var loaded = await store.LoadAsync(path); Assert.Equal(3, loaded.Versions.Count); Assert.Equal(14393.74m, loaded.Result!.Interest); var copy = ProjectStore.Copy(f); Assert.Empty(copy.Versions); Assert.DoesNotContain(copy.Project.Events, e => e.Kind == EventKind.Payment); Assert.NotEqual(p.Id, copy.Project.Id); } finally { Directory.Delete(dir, true); }
    }
}
