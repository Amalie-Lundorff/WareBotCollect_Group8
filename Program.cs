// Program.cs starts the application.
using Avalonia;
using System;

namespace SystemLogin;

// Starts the program.
class Program
{
    // Running the program for Windows.
    [STAThread]
    // Main method and the program starts here. Start the avalonia application.
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    
    // Method for preparing the avalonia before the app starts.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
