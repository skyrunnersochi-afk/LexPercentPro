using System.Net;
using System.Net.Http;
using LexPercent.Core;
using LexPercent.Domain;
using LexPercent.Storage;

namespace LexPercent.Tests;

public class CbrTests
{
    const string Xml = "<KeyRate><KR><DT>2025-01-06T00:00:00+03:00</DT><Rate>73.00</Rate></KR><KR><DT>2025-01-01T00:00:00+03:00</DT><Rate>36.50</Rate></KR><KR><DT>2025-01-02T00:00:00+03:00</DT><Rate>36.50</Rate></KR><KR><DT>2025-01-10T00:00:00+03:00</DT><Rate>73.00</Rate></KR></KeyRate>";
    [Fact]
    public void DailyRatesBecomeDistinctPeriods()
    {
        var s = CbrRates.Parse(Xml); Assert.Equal(2, s.Rates.Length); Assert.Equal(new DateOnly(2025, 1, 1), s.Rates[0].Start); Assert.Equal(36.5m, s.Rates[0].Percent); Assert.Equal(new DateOnly(2025, 1, 10), s.Through);
    }
    [Fact]
    public void ImportedRatesDriveCalculationAndPreserveMoneyEvents()
    {
        var accrual = new CalculationEvent(Guid.NewGuid(), new(2025, 1, 1), EventKind.Accrual, 1000);
        var p = new CalculationProject { Start = new(2025, 1, 1), End = new(2025, 1, 10), FixedRate = true, Events = [accrual] };
        var updated = CbrRates.Apply(p, CbrRates.Parse(Xml));
        Assert.False(updated.FixedRate); Assert.Contains(accrual, updated.Events); Assert.Equal(15m, new Calculator().Calculate(updated).Interest);
        Assert.Equal(updated.Events, CbrRates.Apply(updated, CbrRates.Parse(Xml)).Events);
    }
    [Fact]
    public void EmptyOrDuplicateResponseIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => CbrRates.Parse("<KeyRate/>"));
        Assert.Throws<InvalidDataException>(() => CbrRates.Parse("<KeyRate><KR><DT>2025-01-01</DT><Rate>10</Rate></KR><KR><DT>2025-01-01</DT><Rate>11</Rate></KR></KeyRate>"));
    }
    [Fact]
    public async Task FailedRefreshKeepsOfflineCache()
    {
        var folder = Path.Combine(Path.GetTempPath(), "cbr-test-" + Guid.NewGuid()); var cache = Path.Combine(folder, "rates.json");
        try
        {
            var handler = new ResponseHandler(); var client = new CbrRates(new HttpClient(handler), cache);
            await client.Update(); var before = File.ReadAllText(cache); Assert.Equal(2, client.Load()!.Rates.Length);
            handler.Fail = true; await Assert.ThrowsAsync<HttpRequestException>(() => client.Update()); Assert.Equal(before, File.ReadAllText(cache));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    sealed class ResponseHandler : HttpMessageHandler
    {
        public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("www.cbr.ru", request.RequestUri!.Host); Assert.Equal(HttpMethod.Post, request.Method);
            return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Xml) });
        }
    }
}
