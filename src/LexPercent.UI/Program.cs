using Avalonia;
using LexPercent.UI;
internal static class Program { [STAThread] public static void Main(string[] args) { App.Arguments = args; AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args); } }
