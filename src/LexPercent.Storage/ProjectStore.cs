using System.Text.Json;
using LexPercent.Domain;
namespace LexPercent.Storage;
/// <summary>Versioned single-file storage with atomic replacement and recovery copies.</summary>
public sealed class ProjectStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    public async Task<ProjectFile> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<ProjectFile>(stream, Json) ?? throw new InvalidDataException("Файл проекта пуст.");
        if (file.FormatVersion != 1) throw new InvalidDataException("Версия файла не поддерживается. Откройте его в соответствующей версии программы.");
        if (file.Project is null || file.Project.Events is null || file.Versions is null) throw new InvalidDataException("Нарушена структура файла проекта.");
        return file;
    }
    public async Task<ProjectFile> SaveAsync(string path, ProjectFile file, bool recovery = false, string? description = null)
    {
        file = Clone(file); var versions = file.Versions.ToList();
        if (!recovery && (versions.Count == 0 || JsonSerializer.Serialize(versions[^1].Project, Json) != JsonSerializer.Serialize(file.Project, Json) || description != null))
            versions.Add(new(versions.Count == 0 ? 1 : versions.Max(v => v.Number) + 1, DateTimeOffset.Now, description ?? (versions.Count == 0 ? "Первое сохранение" : Compare(versions[^1].Project, file.Project)), Clone(file.Project), file.Result));
        file = file with { Versions = versions };
        string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            { await JsonSerializer.SerializeAsync(stream, file, Json); await stream.FlushAsync(); stream.Flush(true); }
            await LoadAsync(temp);
            if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true); else File.Move(temp, full);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return file;
    }
    public ProjectFile Restore(ProjectFile file, int number)
    {
        var v = file.Versions.Single(x => x.Number == number); var versions = file.Versions.ToList();
        versions.Add(new(versions.Max(x => x.Number) + 1, DateTimeOffset.Now, $"Восстановлена версия {number}", Clone(v.Project), v.Result));
        return file with { Project = Clone(v.Project), Result = v.Result, Versions = versions };
    }
    public static ProjectFile Copy(ProjectFile file, bool clearPayments = true) => new() { Project = Clone(file.Project) with { Id = Guid.NewGuid(), Events = file.Project.Events.Where(e => !clearPayments || e.Kind != EventKind.Payment).Select(e => e with { Id = Guid.NewGuid() }).ToList() } };
    public static string Compare(CalculationProject a, CalculationProject b)
    {
        var changes = new List<string>();
        if (a.Debtor != b.Debtor) changes.Add($"Должник: {a.Debtor} → {b.Debtor}");
        if (a.Number != b.Number || a.Basis != b.Basis || a.CalculationDate != b.CalculationDate || a.Organization != b.Organization || a.Manager != b.Manager || a.Executor != b.Executor) changes.Add("Общие сведения изменены");
        if (a.Start != b.Start || a.End != b.End || a.ScheduleEnd != b.ScheduleEnd || a.FixedRate != b.FixedRate || a.FixedRatePercent != b.FixedRatePercent || a.FirstAmount != b.FirstAmount || a.MonthlyAmount != b.MonthlyAmount || a.MonthlyDay != b.MonthlyDay || a.JanuaryDay != b.JanuaryDay) changes.Add("Параметры расчета изменены");
        var old = a.Events.ToDictionary(e => e.Id); var next = b.Events.ToDictionary(e => e.Id);
        foreach (var e in b.Events) if (!old.TryGetValue(e.Id, out var v)) changes.Add($"Добавлено: {e.DateText} {e.KindText} {e.ValueText}"); else if (e != v) changes.Add($"Изменено: {v.DateText} {v.KindText} {v.ValueText} → {e.DateText} {e.KindText} {e.ValueText}");
        foreach (var e in a.Events.Where(e => !next.ContainsKey(e.Id))) changes.Add($"Удалено: {e.DateText} {e.KindText} {e.ValueText}");
        if (!a.Signatories.SequenceEqual(b.Signatories)) changes.Add("Подписанты изменены");
        if (a.Document != b.Document) changes.Add("Оформление документа изменено");
        return changes.Count == 0 ? "Без изменений" : string.Join("\n", changes);
    }
}
public sealed record AppSettings
{
    public List<Signatory> Signatories { get; init; } = [Signatory.Moroz];
    public bool Initialized { get; init; }
    public string Organization { get; init; } = "";
    public string Manager { get; init; } = "";
    public string ProjectDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LexPercent Pro");
    public int AutosaveMinutes { get; init; } = 5;
    public DocumentOptions Document { get; init; } = new();
    public List<string> Recent { get; init; } = [];
}
public static class SettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LexPercentPro");
    public static string RecoveryPath => Path.Combine(DirectoryPath, "recovery.lpp");
    public static AppSettings Load()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), ProjectStore.Json) ?? new(); } catch (JsonException) { return new(); }
    }
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath); var path = Path.Combine(DirectoryPath, "settings.json"); var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, ProjectStore.Json)); File.Move(temp, path, true);
    }
}
