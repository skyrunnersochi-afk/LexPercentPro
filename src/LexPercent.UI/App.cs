using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
namespace LexPercent.UI;

public sealed class App : Application
{
    public static string[] Arguments = [];
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new StyleInclude(new Uri("avares://LexPercent.UI/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") }); }
    public override void OnFrameworkInitializationCompleted() { if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow(); base.OnFrameworkInitializationCompleted(); }
}
