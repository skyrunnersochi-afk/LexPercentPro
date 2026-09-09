using System.Globalization;
using LexPercent.Core;
namespace LexPercent.Tests;

public class ReferenceRowsTests
{
    [Fact]
    public void EveryIntervalMatchesUserApprovedSpreadsheet()
    {
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "approved-reference.csv")).Skip(1).ToArray();
        var actual = new Calculator().Calculate(ReferenceExample.Create()).Intervals;
        Assert.Equal(rows.Length, actual.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            var expected = rows[i].Split(','); var r = actual[i];
            Assert.Equal(expected[0], r.Start.ToString("dd.MM.yyyy")); Assert.Equal(expected[1], r.End.ToString("dd.MM.yyyy"));
            Assert.Equal(decimal.Parse(expected[2], CultureInfo.InvariantCulture), r.Debt);
            Assert.Equal(decimal.Parse(expected[3], CultureInfo.InvariantCulture), r.Rate);
            Assert.Equal(int.Parse(expected[4]), r.Days);
            Assert.Equal(decimal.Parse(expected[5], CultureInfo.InvariantCulture), r.Interest);
        }
    }
}
