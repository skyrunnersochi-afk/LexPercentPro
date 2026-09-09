namespace LexPercent.Documents;

public static class MoneyWords
{
    static string Form(int n, string one, string few, string many) => n % 100 is >= 11 and <= 14 ? many : n % 10 == 1 ? one : n % 10 is >= 2 and <= 4 ? few : many;
    public static string Rubles(decimal value)
    {
        if (value < 0) return "Минус " + Rubles(-value).ToLowerInvariant();
        value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        var whole = decimal.Truncate(value); int kopecks = (int)((value - whole) * 100);
        string[] ones = ["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];
        string[] teens = ["десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"];
        string[] tens = ["", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];
        string[] hundreds = ["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];
        string[] scales = ["", "тысяча", "миллион", "миллиард", "триллион", "квадриллион", "квинтиллион", "секстиллион", "септиллион", "октиллион"];
        var groups = new List<string>(); var remaining = whole;
        for (int scale = 0; remaining > 0; scale++, remaining = decimal.Truncate(remaining / 1000))
        {
            int n = (int)(remaining % 1000); if (n == 0) continue;
            var words = new List<string>(); if (n >= 100) words.Add(hundreds[n / 100]);
            int tail = n % 100;
            if (tail is >= 10 and <= 19) words.Add(teens[tail - 10]);
            else { if (tail >= 20) words.Add(tens[tail / 10]); if (tail % 10 > 0) words.Add(scale == 1 && tail % 10 == 1 ? "одна" : scale == 1 && tail % 10 == 2 ? "две" : ones[tail % 10]); }
            if (scale == 1) words.Add(Form(n, "тысяча", "тысячи", "тысяч"));
            else if (scale > 1) words.Add(Form(n, scales[scale], scales[scale] + "а", scales[scale] + "ов"));
            groups.Insert(0, string.Join(" ", words));
        }
        var result = (whole == 0 ? "ноль" : string.Join(" ", groups)) + " " + Form((int)(whole % 100), "рубль", "рубля", "рублей") + $" {kopecks:00} " + Form(kopecks, "копейка", "копейки", "копеек");
        return char.ToUpperInvariant(result[0]) + result[1..];
    }
}
