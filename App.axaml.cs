// App.axaml.cs controls how the app should start. 
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Login; 

// Defines where the class belong. Matches with the class in app.axaml.
namespace SystemLogin;

// This is one complete class that is spread in the files app.axaml and app.axaml.cs
public partial class App : Application
{
    // When the app starts, this method runs and applies the styling and themes.
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    
    public override void OnFrameworkInitializationCompleted()
    {
        // This method checks if it is a desktop application. 
        // It also creates the main window and displays it. 
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        // Calls the method som the base class, så Avalonia can finish starting the app. 
        base.OnFrameworkInitializationCompleted();
    }

}
