using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LexPercent.Domain;

namespace LexPercent.Storage;

public sealed record CbrRate(DateOnly Start, decimal Percent);
public sealed record CbrSnapshot(DateTimeOffset Updated, DateOnly Through, CbrRate[] Rates);

public sealed class CbrRates
{
    public const string Source = "ЦБ РФ · официальный справочник";
    public const string Endpoint = "https://www.cbr.ru/DailyInfoWebServ/DailyInfo.asmx";
    private readonly HttpClient http;
    private readonly string cache;
    public CbrRates(HttpClient http, string cache) { this.http = http; this.cache = cache; }
    public CbrSnapshot? Load()
    {
        try { return JsonSerializer.Deserialize<CbrSnapshot>(File.ReadAllText(cache)); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
    public async Task<CbrSnapshot> Update(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        string xml = $"<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><KeyRateXML xmlns=\"http://web.cbr.ru/\"><fromDate>2013-09-13T00:00:00</fromDate><ToDate>{today:yyyy-MM-dd}T00:00:00</ToDate></KeyRateXML></soap:Body></soap:Envelope>";
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };
        request.Headers.Add("SOAPAction", "\"http://web.cbr.ru/KeyRateXML\"");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var snapshot = Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        var temp = cache + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(snapshot), cancellationToken);
        File.Move(temp, cache, true);
        return snapshot;
    }
    public static CbrSnapshot Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var daily = doc.Descendants().Where(e => e.Name.LocalName == "KR").Select(e =>
        {
            var date = e.Elements().Single(x => x.Name.LocalName == "DT").Value;
            var value = e.Elements().Single(x => x.Name.LocalName == "Rate").Value;
            return new CbrRate(DateOnly.ParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture), decimal.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture));
        }).OrderBy(r => r.Start).ToArray();
        if (daily.Length == 0 || daily.Any(r => r.Percent < 0) || daily.Select(r => r.Start).Distinct().Count() != daily.Length)
            throw new InvalidDataException("ЦБ вернул пустую или некорректную историю ставок. Сохранённый справочник оставлен без изменений.");
        var changes = new List<CbrRate>();
        foreach (var r in daily) if (changes.Count == 0 || changes[^1].Percent != r.Percent) changes.Add(r);
        return new(DateTimeOffset.Now, daily[^1].Start, changes.ToArray());
    }
    public static CalculationProject Apply(CalculationProject project, CbrSnapshot snapshot)
    {
        var start = project.Events.Where(e => e.Kind == EventKind.Accrual).Select(e => e.Date).Append(project.Start).Min();
        var first = snapshot.Rates.LastOrDefault(r => r.Start <= start);
        if (first is null) throw new ArgumentException("В справочнике нет ставки на начало расчёта. Введите её вручную.");
        if (project.End > DateOnly.FromDateTime(snapshot.Updated.LocalDateTime) && project.End > snapshot.Through) throw new ArgumentException($"Справочник содержит данные по {snapshot.Through:dd.MM.yyyy}. Для более поздней даты обновите данные или задайте ставку вручную.");
        var relevant = snapshot.Rates.Where(r => r.Start > start && r.Start <= project.End).Prepend(first);
        var events = project.Events.Where(e => e.Kind != EventKind.Rate).ToList();
        foreach (var r in relevant)
        {
            var existing = project.Events.FirstOrDefault(e => e.Kind == EventKind.Rate && e.Date == r.Start);
            events.Add(new(existing?.Id ?? Guid.NewGuid(), r.Start, EventKind.Rate, r.Percent, Source));
        }
        return project with { FixedRate = false, Events = events.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList() };
    }
}
