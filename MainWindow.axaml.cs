using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using SystemLogin;

namespace Login;

// MainWindow er code-behind til MainWindow.axaml.
// INotifyPropertyChanged bruges til databinding, så GUI opdateres automatisk.
// RelayCommand er en måde at forbinde en knap i GUI med en metode i koden – uden at bruge Click-events. Indeholder: Hvad skal køres + må det køres..
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Service til login og brugerhåndtering
    private AccountService _accountService;
    // Database-kontekst (Entity Framework)
    private AppDbContext _appDbContext;
    // Den aktuelt loggede bruger (null hvis ingen er logget ind)
    private Account? _currentUser;

    // Viser tidligere ordrer i Admin-fanen
    public ObservableCollection<OrderViewModel> PreviousOrders { get; } = new();
    // Viser tilgængelige produkter i Create Order
    public ObservableCollection<ProductViewModel> AvailableProducts { get; } = new();
    // Viser produkter i den aktuelle ordre (kurv)
    public ObservableCollection<ProductViewModel> OrderLines { get; } = new();
    // Viser ordrelinjer i Database-fanen
    public ObservableCollection<OrderLine> DatabaseOrderLines { get; } = new();
    // Bruges til at vise seneste ordre i Database-fanen
    public ObservableCollection<ProductViewModel> LastOrderProducts { get; } = new();

    // Starter behandling af ordren (fx robot)
    public RelayCommand ProcessOrderCommand { get; }
    // Fjerner seneste ordre fra databasen
    public RelayCommand ConfirmOrderCommand { get; }

    // Event der bruges til at opdatere UI, når properties ændres
    public event PropertyChangedEventHandler? PropertyChanged;
    // Kaldes når en property ændres
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Viser brugerens ID i UI
    public string CurrentUserId => _currentUser?.Username ?? "";
    
    // Bruges til at vise/skjule UI-elementer
    public bool IsLoggedIn => _currentUser != null;
    public bool IsLoggedOut => _currentUser == null;

    // Tilføjer produkt til ordren
    public RelayCommand<ProductViewModel> AddToOrderCommand { get; }
    // Fjerner produkt fra ordren
    public RelayCommand<ProductViewModel> RemoveFromOrderCommand { get; }
    // Øger mængde på produkt
    public RelayCommand<ProductViewModel> IncreaseQtyCommand { get; }
    // Mindsker mængde på produkt
    public RelayCommand<ProductViewModel> DecreaseQtyCommand { get; }
    // Afsender ordren
    public RelayCommand PlaceOrderCommand { get; }

    // Holder styr på hvilken fane der er valgt
    private int _selectedTabIndex;
    // Når Database-fanen vælges, indlæses seneste ordre
    public int SelectedTabIndex
    {
        // Returnerer den aktuelt valgte fane
        get => _selectedTabIndex;
        // Kører hver gang brugeren skifter fane
        set
        {
            // Opdaterer intern variabel med ny fane
            _selectedTabIndex = value;
            // Giver besked til UI om at værdien er ændret
            // (opdaterer databinding)
            OnPropertyChanged();

            // Tjekker om Database-fanen er valgt
            // 2 svarer til Database-tab i TabControl
            if (_selectedTabIndex == 2)
                // Indlæser den seneste ordre fra databasen
                LoadLatestOrderFromDatabase();
        }
    }

    // Initialiserer UI, services, commands og standarddata
    public MainWindow()
    {
        InitializeComponent();
        // Gør denne klasse til DataContext for XAML
        DataContext = this;

        // Initialiserer services og database
        InitializeServices();
        // Kører når vinduet er loadet
        Loaded += OnLoaded;

        // Commands til ændring af mængder
        // Command til at øge mængden af et produkt med 1
        IncreaseQtyCommand = new RelayCommand<ProductViewModel>(p => p.Quantity++);
        // Command til at mindske mængden af et produkt
        // Forhindrer at mængden bliver negativ
        DecreaseQtyCommand = new RelayCommand<ProductViewModel>(p =>
        {
            if (p.Quantity > 0)
                p.Quantity--;
        });

        // Command til at tilføje et produkt til ordren (kurven)
        AddToOrderCommand = new RelayCommand<ProductViewModel>(AddToOrder); // Command til at tilføje et produkt til ordren (kurven)
        RemoveFromOrderCommand = new RelayCommand<ProductViewModel>(RemoveFromOrder); // Command til at fjerne et produkt fra ordren
        // Command til at afsende ordren
        // Kan kun køres hvis brugeren er logget ind
        // og der findes mindst ét produkt med mængde > 0
        PlaceOrderCommand = new RelayCommand(
            PlaceOrder,
            () => IsLoggedIn && OrderLines.Any(p => p.Quantity > 0)
        );

        // Commands til database og robot
        ProcessOrderCommand = new RelayCommand(OnConfirmOrderCompleted);
        ConfirmOrderCommand = new RelayCommand(OnConfirmOrderRemoveFromDatabase);

        // Opretter standardprodukter
        AvailableProducts.Add(new ProductViewModel("Component A"));
        AvailableProducts.Add(new ProductViewModel("Component B"));
        AvailableProducts.Add(new ProductViewModel("Component C"));
    }

    // Kører ved opstart og opretter databasen hvis den ikke findes
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (await EnsureDatabaseCreatedWithExampleDataAsync())
            Log("Database did not exist. Created a new one.");
    }

    // Initialiserer database og account-service
    private void InitializeServices()
    {
        // Lukker evt. eksisterende database-forbindelse
        _appDbContext?.Dispose();
        // Opretter ny database-kontekst
        _appDbContext = new AppDbContext();
        // Opretter service til login og brugerstyring
        _accountService = new AccountService(_appDbContext, new PasswordHasher());
    }
    
    // Sikrer at databasen findes og opretter eksempelbrugere
    private async Task<bool> EnsureDatabaseCreatedWithExampleDataAsync()
    {
        // Opretter databasen hvis den ikke findes
        var created = await _appDbContext.Database.EnsureCreatedAsync();
        // Hvis databasen allerede fandtes, stoppes metoden
        if (!created) return false;

        // Geninitialiserer services efter database-oprettelse
        InitializeServices();
        // Opretter standard admin-bruger
        await _accountService.NewAccountAsync("admin", "admin", true);
        // Opretter standard normal bruger
        await _accountService.NewAccountAsync("user", "user");
        // Returnerer true hvis databasen blev oprettet
        return true;
    }
    // Kaldes når Database-fanen vælges
    private void OnDatabaseTabSelected()
    {
        // Indlæser den seneste ordre fra databasen
        LoadLatestOrderFromDatabase();
    }
    
    // Logger brugeren ind og opdaterer UI
    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Indlæser evt. tidligere ordrer (sikkerhedsopdatering)
        LoadOrdersForUser(_currentUser);

        // Indlæser seneste ordre til Database-fanen
        LoadLatestOrderFromDatabase();

        // Opdaterer om knapper må bruges (CanExecute)
        UpdateCanExecute();

        // Henter brugernavn og adgangskode fra inputfelter
        var username = LoginUsername.Text;
        var password = LoginPassword.Text;

        // Tjekker om brugernavnet findes i databasen
        if (!await _accountService.UsernameExistsAsync(username))
        {
            // Skriver fejl i loggen og stopper login
            Log("Username does not exist.");
            return;
        }
        // Tjekker om adgangskoden er korrekt
        if (!await _accountService.CredentialsCorrectAsync(username, password))
        {
            // Skriver fejl i loggen og stopper login
            Log("Password wrong.");
            return;
        }

        // Henter brugeren fra databasen og sætter som aktiv bruger
        _currentUser = await _accountService.GetAccountAsync(username);

        // Viser logout-knappen
        LogoutButton.IsVisible = true;

        // Skriver login-besked i loggen
        Log($"{_currentUser.Username} logged in.");

        // Rydder inputfelter efter login
        LoginUsername.Text = "";
        LoginPassword.Text = "";

        // Opdaterer UI så elementer reagerer på login-status
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsLoggedOut));
        OnPropertyChanged(nameof(CurrentUserId));

        // Opdaterer hvilke commands der er aktive
        UpdateCanExecute();

        // Indlæser brugerens ordrer efter login
        LoadOrdersForUser(_currentUser);

        // Opdaterer commands igen efter dataændringer
        UpdateCanExecute();
    }


    // Logger brugeren ud og rydder data
    private void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentUser = null;
        PreviousOrders.Clear();
        LogoutButton.IsVisible = false;

        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsLoggedOut));
        OnPropertyChanged(nameof(CurrentUserId));

        Log("Logged out.");
        UpdateCanExecute();
    }

    private void OnGoToLoginClick(object? sender, RoutedEventArgs e)
    {
        SelectedTabIndex = 0;
    }

    private void AddToOrder(ProductViewModel product)
    {
        if (product.Quantity <= 0) return;

        OrderLines.Add(new ProductViewModel(product.Name, product.Quantity));
        product.Quantity = 0;

        UpdateCanExecute();
    }

    private void RemoveFromOrder(ProductViewModel product)
    {
        OrderLines.Remove(product);
        UpdateCanExecute();
    }

    private async void PlaceOrder()
    {
        if (_currentUser == null || !OrderLines.Any())
        {
            Log("You must be logged in and have items in the cart.");
            return;
        }

        var order = new Order
        {
            AccountUsername = _currentUser.Username,
            CreatedAt = DateTime.Now,
            OrderLines = OrderLines.Select(p => new OrderLine
            {
                ProductName = p.Name,
                Quantity = p.Quantity
            }).ToList()
        };


        _appDbContext.Orders.Add(order);
        await _appDbContext.SaveChangesAsync();
        LoadLatestOrderFromDatabase();

        Log("Order placed and saved to database.");
        foreach (var line in order.OrderLines)
            Log($" - {line.ProductName} x{line.Quantity}");

        // Overfør til databasen-fanen
        LastOrderProducts.Clear();
        foreach (var item in OrderLines)
            LastOrderProducts.Add(new ProductViewModel(item.Name, item.Quantity));

        // Ryd kurven
        OrderLines.Clear();

        // Genindlæs ordrer
        LoadOrdersForUser(_currentUser);

        // Skift til "Database"-fanen
        SelectedTabIndex = 2;

        UpdateCanExecute();
    }

// Indlæser den seneste ordre fra databasen
// og viser den i Database-fanen
    private void LoadLatestOrderFromDatabase()
    {
        // Rydder den nuværende visning af ordrelinjer i UI
        DatabaseOrderLines.Clear();

        // Henter den nyeste ordre fra databasen
        // Include sikrer at ordrelinjer også indlæses
        var order = _appDbContext.Orders
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        // Hvis der ikke findes nogen ordre, afsluttes metoden
        if (order == null)
            return;

        // Gennemgår alle ordrelinjer i den seneste ordre
        foreach (var line in order.OrderLines)
        {
            // Tilføjer hver ordrelinje til ObservableCollection
            // så UI automatisk opdateres
            DatabaseOrderLines.Add(line);
        }
    }

// Indlæser alle ordrer for den aktuelle bruger
// og viser dem i Admin-fanen
    private void LoadOrdersForUser(Account user)
    {
        // Stopper hvis brugeren ikke findes eller ikke har et brugernavn
        if (user == null || string.IsNullOrEmpty(user.Username))
            return;

        // Rydder tidligere viste ordrer i UI
        PreviousOrders.Clear();

        // Henter alle ordrer fra databasen for den valgte bruger
        // Include bruges for også at hente ordrelinjer
        var orders = _appDbContext.Orders
            .Where(o => o.AccountUsername == user.Username)
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        // Gennemgår hver ordre og konverterer den til et ViewModel
        foreach (var order in orders)
        {
            PreviousOrders.Add(new OrderViewModel
            {
                // Viser ordrenummer
                OrderId = order.Id,

                // Viser oprettelsesdato i læsevenligt format
                CreatedAt = order.CreatedAt.ToShortDateString(),

                // Beregner samlet antal produkter i ordren
                TotalQuantity = order.OrderLines.Sum(l => l.Quantity)
            });
        }
    }

// Opretter en ny bruger, når "Create user"-knappen trykkes
    private void CreateUserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Henter brugernavn fra inputfeltet
        var username = CreateUserUsername.Text;

        // Henter password fra inputfeltet
        var password = CreateUserPassword.Text;

        // Aflæser om brugeren skal være admin
        // ?? false sikrer, at værdien ikke bliver null
        var isAdmin = CreateUserIsAdmin.IsChecked ?? false;

        // Tjekker om brugernavn eller password mangler
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Log("Username and password are required.");
            return;
        }

        // Tjekker om brugernavnet allerede findes i databasen
        if (_accountService.UsernameExistsAsync(username).Result)
        {
            Log("Username already exists.");
            return;
        }

        // Opretter den nye bruger i databasen
        _accountService.NewAccountAsync(username, password, isAdmin).Wait();

        // Skriver besked i loggen
        Log($"User '{username}' created (Admin: {isAdmin})");

        // Rydder inputfelter efter oprettelse
        CreateUserUsername.Text = "";
        CreateUserPassword.Text = "";
        CreateUserIsAdmin.IsChecked = false;
    }

// Sender den aktuelle ordre til robotten
    private void OnConfirmOrderCompleted()
    {
        // Kører robot-kommandoen i en baggrundstråd
        // så UI ikke fryser
        Task.Run(() =>
        {
            // Tæller samlet antal af hver komponent i ordren
            int a = DatabaseOrderLines
                .Where(x => x.ProductName == "Component A")
                .Sum(x => x.Quantity);

            int b = DatabaseOrderLines
                .Where(x => x.ProductName == "Component B")
                .Sum(x => x.Quantity);

            int c = DatabaseOrderLines
                .Where(x => x.ProductName == "Component C")
                .Sum(x => x.Quantity);

            // Sender én samlet ordre til robotten
            // (antal A, B og C)
            RobotConnectionTest.RunOrder(a, b, c);
        });

        // Logger handlingen i UI
        Log("Order sent to robot (one combined program)...");
    }
// Fjerner den seneste ordre fra databasen
// efter den er blevet behandlet
    private async void OnConfirmOrderRemoveFromDatabase()
    {
        // Finder den nyeste ordre i databasen
        var latestOrder = await _appDbContext.Orders
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        // Hvis der ikke findes nogen ordre, stop
        if (latestOrder == null)
        {
            Log("No order to remove.");
            return;
        }

        // Fjerner først alle ordrelinjer
        _appDbContext.OrderLines.RemoveRange(latestOrder.OrderLines);

        // Fjerner selve ordren
        _appDbContext.Orders.Remove(latestOrder);

        // Gemmer ændringerne i databasen
        await _appDbContext.SaveChangesAsync();

        // UI-opdatering skal ske på UI-thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Rydder visningen i Database-fanen
            DatabaseOrderLines.Clear();

            // Opdaterer listen over tidligere ordrer
            LoadOrdersForUser(_currentUser);
        });

        // Logger handlingen
        Log("Latest order removed from database.");
    }
    
// Sletter databasen og opretter den igen med eksempeldata
    private void RecreateDatabaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Sletter databasen asynkront (ignorerer returværdi)
        _ = _appDbContext.Database.EnsureDeletedAsync();

        // Opretter databasen igen og indsætter eksempelbrugere
        _ = EnsureDatabaseCreatedWithExampleDataAsync();

        // Skriver besked i loggen
        Log("Database recreated.");
    }

// Skifter til fanen "Create Order", når knappen trykkes
    private void OnPlaceOrderClick(object? sender, RoutedEventArgs e)
    {
        // Logger handlingen
        Log("Opened new order page.");

        // 3 svarer til fanen "Create Order"
        SelectedTabIndex = 3;
    }

// Rydder log-visningen i UI
    private void ClearLogButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Fjerner al tekst i log-feltet
        LogOutput.Text = "";
    }

// Opdaterer om kommandoer (fx PlaceOrder) må udføres
    private void UpdateCanExecute()
    {
        // Tvinger UI til at genberegne CanExecute
        PlaceOrderCommand?.RaiseCanExecuteChanged();
    }

// Eksempel på en simpel klik-handler (bruges evt. til test)
    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        // Skriver besked i konsollen
        Console.WriteLine("Process order button clicked.");
    }

    // Skriver en besked til loggen med tidspunkt
    private void Log(string message)
    {
        // Tilføjer tidsstempel og besked til log-output
        LogOutput.Text += $"{DateTime.Now:HH:mm:ss} | {message}\n";
    }


// === ViewModel for visning af ordrer i UI ===
// Bruges i Admin-fanen til at vise tidligere ordrer
    public class OrderViewModel
    {
        // Ordre-ID fra databasen
        // Bruges til identifikation og visning i UI
        public int OrderId { get; set; }

        // Dato for hvornår ordren blev oprettet
        // Gemmes som string for nem visning i UI
        public string CreatedAt { get; set; } = "";

        // Samlet antal produkter i ordren
        // Beregnes ud fra ordrelinjerne
        public int TotalQuantity { get; set; }
    }


// ViewModel for et produkt i UI
// Implementerer INotifyPropertyChanged så UI automatisk opdateres
    public class ProductViewModel : INotifyPropertyChanged
    {
        // Navnet på produktet (fx "Component A")
        // Har kun getter, da navnet ikke ændres efter oprettelse
        public string Name { get; }

        // Privat backing field til Quantity
        private int _quantity;

        // Antal af produktet
        // Bruges i UI til at vise og ændre mængde
        public int Quantity
        {
            get => _quantity;
            set
            {
                // Sikrer at UI kun opdateres hvis værdien ændres
                if (_quantity != value)
                {
                    _quantity = value;

                    // Giver besked til UI om at Quantity er ændret
                    OnPropertyChanged();
                }
            }
        }

        // Constructor der sætter navn og start-antal
        public ProductViewModel(string name, int quantity = 0)
        {
            Name = name;
            Quantity = quantity;
        }

        // Event som UI lytter på for ændringer
        public event PropertyChangedEventHandler? PropertyChanged;

        // Metode der udløser PropertyChanged-eventet
        // [CallerMemberName] betyder at property-navnet sendes automatisk
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

// === RelayCommand ===
// Generel command-klasse der bruges til at forbinde knapper i UI
// med metoder i ViewModel (MVVM-mønster)
    public class RelayCommand : ICommand
    {
        // Metoden der udføres når commanden aktiveres
        private readonly Action _execute;

        // Valgfri logik der bestemmer om commanden må udføres
        private readonly Func<bool>? _canExecute;

        // Constructor der modtager:
        // - execute: hvad der skal ske ved klik
        // - canExecute: hvornår knappen må være aktiv
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        // Event som UI lytter på for at opdatere knappers enabled/disabled-tilstand
        public event EventHandler? CanExecuteChanged;

        // Kaldes af UI for at tjekke om commanden må udføres
        public bool CanExecute(object? parameter)
            => _canExecute == null || _canExecute();

        // Kaldes når brugeren klikker på knappen
        public void Execute(object? parameter)
            => _execute();

        // Tvinger UI til at genberegne CanExecute
        // Bruges når data ændrer sig (fx login eller ordreindhold)
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

// === RelayCommand<T> ===
// Generisk command-klasse der bruges når en command
// skal modtage et parameter (fx et produkt)
    public class RelayCommand<T> : ICommand
    {
        // Metoden der udføres ved klik og modtager et parameter af typen T
        private readonly Action<T> _execute;

        // Valgfri logik der bestemmer om commanden må udføres
        private readonly Func<T, bool>? _canExecute;

        // Constructor der modtager:
        // - execute: hvad der skal ske ved klik (med parameter)
        // - canExecute: hvornår commanden må udføres
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        // Event som UI lytter på for at opdatere enabled/disabled-tilstand
        public event EventHandler? CanExecuteChanged;

        // Kaldes af UI for at tjekke om commanden må udføres
        // Tjekker samtidig om parameteret er af typen T
        public bool CanExecute(object? parameter)
            => _canExecute == null || parameter is T;

        // Kaldes når brugeren klikker på knappen
        // Parameteret castes til T før metoden udføres
        public void Execute(object? parameter)
        {
            if (parameter is T value)
                _execute(value);
        }

        // Tvinger UI til at genberegne CanExecute
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}    
