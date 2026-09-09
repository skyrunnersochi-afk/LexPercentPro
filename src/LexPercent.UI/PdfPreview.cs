using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LexPercent.Documents;

namespace LexPercent.UI;

/// <summary>Lazy single-page rendering bounds memory for long protocols.</summary>
public sealed class PdfPreview : StackPanel
{
    private readonly byte[] pdf;
    private readonly Image image = new() { Stretch = Stretch.Uniform, MaxWidth = 900 };
    private readonly TextBlock label = new();
    private Bitmap? bitmap;
    private int page;
    private readonly int count;

    public PdfPreview(Report report)
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try { Exporter.Export(temp, report); pdf = File.ReadAllBytes(temp); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        count = PDFtoImage.Conversion.GetPageCount(pdf);
        var controls = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        var previous = new Button { Content = "←" };
        var next = new Button { Content = "→" };
        controls.Children.Add(previous); controls.Children.Add(label); controls.Children.Add(next);
        Children.Add(controls); Children.Add(image);
        void Show()
        {
            using var png = new MemoryStream();
            PDFtoImage.Conversion.SavePng(png, pdf, page: page, options: new(Dpi: 110));
            png.Position = 0;
            var replacement = new Bitmap(png); image.Source = replacement; bitmap?.Dispose(); bitmap = replacement;
            label.Text = $"Страница {page + 1} из {count}";
            previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < count;
        }
        previous.Click += (_, _) => { page--; Show(); };
        next.Click += (_, _) => { page++; Show(); };
        DetachedFromVisualTree += (_, _) => { image.Source = null; bitmap?.Dispose(); bitmap = null; };
        Show();
    }
}
