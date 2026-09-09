using LexPercent.Core;
using LexPercent.Documents;
using LexPercent.Domain;
using LexPercent.Storage;

namespace LexPercent.Tests;

public class PrintedFormTests
{
    [Theory]
    [InlineData("48613.26", "Сорок восемь тысяч шестьсот тринадцать рублей 26 копеек")]
    [InlineData("0", "Ноль рублей 00 копеек")]
    [InlineData("1.01", "Один рубль 01 копейка")]
    [InlineData("2.02", "Два рубля 02 копейки")]
    [InlineData("11.11", "Одиннадцать рублей 11 копеек")]
    [InlineData("21002.21", "Двадцать одна тысяча два рубля 21 копейка")]
    [InlineData("2000000.99", "Два миллиона рублей 99 копеек")]
    [InlineData("999.995", "Одна тысяча рублей 00 копеек")]
    public void MoneyIsSpelledCorrectly(string number, string expected) => Assert.Equal(expected, MoneyWords.Rubles(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void PrintedFormHasCenteredCaptionBasisAndChosenSignature()
    {
        var p = ReferenceExample.Create() with { Basis = "Справка от 09.09.2026", Signatories = [Signatory.Moroz] };
        var r = Report.Create(p, new Calculator().Calculate(p));
        Assert.Equal("center", r.Before.Single(b => b.Text == "Расчет процентов за пользование чужими денежными средствами").Style.Align);
        Assert.Contains(r.Before, b => b.Text.Contains("Основание: Справка от 09.09.2026"));
        Assert.Equal(Signatory.Moroz.Name, r.After[^1].RightText);
        Assert.Equal(Signatory.Moroz.Position + "\n" + Signatory.Moroz.Rank, r.After[^1].Text);
        Assert.Contains(r.After, b => b.Text == "Четырнадцать тысяч триста девяносто три рубля 74 копейки");
    }
    [Fact]
    public void ProjectStoresSignerSnapshotIndependentOfDirectory()
    {
        var p = ReferenceExample.Create() with { Signatories = [Signatory.Moroz] };
        var saved = ProjectStore.Clone(p);
        var edited = Signatory.Moroz with { Name = "Другой подписант" };
        Assert.NotEqual(edited, saved.Signatories[0]);
        Assert.Equal("А.И. Мороз", saved.Signatories[0].Name);
        Assert.Contains("Подписанты изменены", ProjectStore.Compare(saved, saved with { Signatories = [edited] }));
    }
}
