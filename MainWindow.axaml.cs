// === MainWindow.axaml.cs controls how the program behaves === 
/* 
This includes:
logging in, creating admin users, showing previous orders,
placing irders, saving orders and  sending order to the robot
*/ 

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

// === This controls the main window that the user sees ===
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // === This handles the log in and users, the link to the database and the data of the logged in user ===
    private AccountService _accountService;
    private AppDbContext _appDbContext;
    private Account? _currentUser;

    // === Automatically updates the lists shown ===. 
    public ObservableCollection<OrderViewModel> PreviousOrders { get; } = new();
    public ObservableCollection<ProductViewModel> AvailableProducts { get; } = new();
    public ObservableCollection<ProductViewModel> OrderLines { get; } = new();
    public ObservableCollection<OrderLine> DatabaseOrderLines { get; } = new();
    public ObservableCollection<ProductViewModel> LastOrderProducts { get; } = new();

    // === Links the buttons to their actions === 
    public RelayCommand ProcessOrderCommand { get; }
    public RelayCommand ConfirmOrderCommand { get; }

    // === Automatically updates the values shown ===
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // === If the user is logged in or out and some actions are available === 
    public string CurrentUserId => _currentUser?.Username ?? "";
    public bool IsLoggedIn => _currentUser != null;
    public bool IsLoggedOut => _currentUser == null;

    // === Links the buttons to their actions ===
    public RelayCommand<ProductViewModel> AddToOrderCommand { get; }
    public RelayCommand<ProductViewModel> RemoveFromOrderCommand { get; }
    public RelayCommand<ProductViewModel> IncreaseQtyCommand { get; }
    public RelayCommand<ProductViewModel> DecreaseQtyCommand { get; }
    public RelayCommand PlaceOrderCommand { get; }

    // === Controls the active tab which is open, so the program knows what to display === 
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();

            // 2 = Database-tab
            if (_selectedTabIndex == 2)
                LoadLatestOrderFromDatabase();
        }
    }

    // === This will run when opening the program === 
    public MainWindow()
    {
        // === Reads the design from the MainWindow.axaml === 
        InitializeComponent();
        DataContext = this;
        
        // === Makes the system ready to working ===
        InitializeServices();
        Loaded += OnLoaded;

        // === When adding/removing a product, the quantity changes. But never below 0 ===
        IncreaseQtyCommand = new RelayCommand<ProductViewModel>(p => p.Quantity++);
        DecreaseQtyCommand = new RelayCommand<ProductViewModel>(p =>
        {
            if (p.Quantity > 0)
                p.Quantity--;
        });

        // === Adding and deleting from the order === 
        AddToOrderCommand = new RelayCommand<ProductViewModel>(AddToOrder);
        RemoveFromOrderCommand = new RelayCommand<ProductViewModel>(RemoveFromOrder);

        // === The place order only works if the user is logged in and there is items in the order === 
        PlaceOrderCommand = new RelayCommand(
            PlaceOrder,
            () => IsLoggedIn && OrderLines.Any(p => p.Quantity > 0)
        );

        // === Sends the order to the robot and deletes the order from the database ===
        ProcessOrderCommand = new RelayCommand(OnConfirmOrderCompleted);
        ConfirmOrderCommand = new RelayCommand(OnConfirmOrderRemoveFromDatabase);

        // === Available products the admin can order ===
        AvailableProducts.Add(new ProductViewModel("Component A"));
        AvailableProducts.Add(new ProductViewModel("Component B"));
        AvailableProducts.Add(new ProductViewModel("Component C"));
    }

    // === When the window opens, it check if the database exists === 
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // === if the database does not exists, it is created ===
        if (await EnsureDatabaseCreatedWithExampleDataAsync())
            Log("Database did not exist. Created a new one.");
    }

    // === Initial actions to prepare the systems that the program needs to work ===
    private void InitializeServices()
    {
        // === Close old database connection === 
        _appDbContext?.Dispose();
        // === Create new database connection === 
        _appDbContext = new AppDbContext();
        // === Create the log in system === 
        _accountService = new AccountService(_appDbContext, new PasswordHasher());
    }

    // === The database acts whether it exists or not ===  
    private async Task<bool> EnsureDatabaseCreatedWithExampleDataAsync()
    {
        // === Check if the database exists. If not, create it and if yes, do nothing ===
        var created = await _appDbContext.Database.EnsureCreatedAsync();
        if (!created) return false;

        // === Establishes a new database so the admin or other users can log in ===
        InitializeServices();
        await _accountService.NewAccountAsync("admin", "admin", true);
        await _accountService.NewAccountAsync("user", "user");
        return true;
    }

    // === Load the latest orders, when the database tab is selected === 
    private void OnDatabaseTabSelected()
    {
        LoadLatestOrderFromDatabase();
    }
    
    // === Log in === 
    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    { 
        LoadOrdersForUser(_currentUser);
        LoadLatestOrderFromDatabase();
        UpdateCanExecute();

        // === Reads the username and password === 
        var username = LoginUsername.Text;
        var password = LoginPassword.Text;

        // === Check if the username exist in the database. If not, show message ===
        if (!await _accountService.UsernameExistsAsync(username))
        {
            Log("Username does not exist.");
            return;
        }

        // === Check if the password is correct for the user. If not, show message === 
        if (!await _accountService.CredentialsCorrectAsync(username, password))
        {
            Log("Password wrong.");
            return;
        }
        
        // === Get the user from the database and store them as the current user === 
        _currentUser = await _accountService.GetAccountAsync(username);

        // === Show log out button as the user is logged in === 
        LogoutButton.IsVisible = true;

        // === Shows the username that is logged in === 
        Log($"{_currentUser.Username} logged in.");

        // === Removes the text in the log in ===
        LoginUsername.Text = "";
        LoginPassword.Text = "";

        // === Update the display when the log in is complete (buttons, text) ===
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsLoggedOut));
        OnPropertyChanged(nameof(CurrentUserId));
        UpdateCanExecute();

        // === Load and display the previous orders made === 
        LoadOrdersForUser(_currentUser);
        UpdateCanExecute();
    }

    // === Log out ===
    private void LogoutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // === Program forgets the user, does not show information for the user and remvoes the log out button === 
        _currentUser = null;
        PreviousOrders.Clear();
        LogoutButton.IsVisible = false;

        // === Update the display when the log out is complete (buttons, text) ===
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsLoggedOut));
        OnPropertyChanged(nameof(CurrentUserId));

        // === Show message === 
        Log("Logged out.");
        UpdateCanExecute();
    }

    // === Go to log in tab === 
    private void OnGoToLoginClick(object? sender, RoutedEventArgs e)
    {
        SelectedTabIndex = 0;
    }

    // === Add products to the order === 
    private void AddToOrder(ProductViewModel product)
    {
        // === Cannot go below 0 ===
        if (product.Quantity <= 0) return;

        // === Add the products to the order === 
        OrderLines.Add(new ProductViewModel(product.Name, product.Quantity));
        product.Quantity = 0;
    
        UpdateCanExecute();
    }

    // === Remove items from the order === 
    private void RemoveFromOrder(ProductViewModel product)
    {
        OrderLines.Remove(product);
        UpdateCanExecute();
    }

    // === Place order button === 
    private async void PlaceOrder()
    {
        // === Stop if the user is not logged in or the cart is empty ===
        if (_currentUser == null || !OrderLines.Any())
        {
            // === Message if above is true ===
            Log("You must be logged in and have items in the cart.");
            return;
        }

        // === Creates the order. Stores the admin, time and quantity ===
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

        // === save the order in the database and show the latest order ===
        _appDbContext.Orders.Add(order);
        await _appDbContext.SaveChangesAsync();
        LoadLatestOrderFromDatabase();

        // === Displays each product ordered === 
        Log("Order placed and saved to database.");
        foreach (var line in order.OrderLines)
            Log($" - {line.ProductName} x{line.Quantity}");

        // Transfer the order to the database tab ===
        LastOrderProducts.Clear();
        foreach (var item in OrderLines)
            LastOrderProducts.Add(new ProductViewModel(item.Name, item.Quantity));

        // === Empties the order === 
        OrderLines.Clear();

        // === Reload the order history === 
        LoadOrdersForUser(_currentUser);

        // === Change to the database tab ===
        SelectedTabIndex = 2;

        UpdateCanExecute();
    }

    // === Show the latest order === 
    private void LoadLatestOrderFromDatabase()
    {
        // === Remove what was shown before === 
        DatabaseOrderLines.Clear();

        // === Finding the latest order === 
        var order = _appDbContext.Orders
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        // === Stop if there are no orders == 
        if (order == null)
            return;

        // == Show all the products in the order === 
        foreach (var line in order.OrderLines)
        {
            DatabaseOrderLines.Add(line);
        }
    }

    // === Previous orders ===
    private void LoadOrdersForUser(Account user)
    {
        // === Check if anyone is logged in or the username is empty ===
        if (user == null || string.IsNullOrEmpty(user.Username))
            return;

        // === Removes orders from other users === 
        PreviousOrders.Clear();

        // === Finding all the orders for this user and shows the newest in the top === 
        var orders = _appDbContext.Orders
            .Where(o => o.AccountUsername == user.Username)
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        // === For all orders, a summary incl. order number, date and quantity is shown === 
        foreach (var order in orders)
        {
            PreviousOrders.Add(new OrderViewModel
            {
                OrderId = order.Id,
                CreatedAt = order.CreatedAt.ToShortDateString(),
                TotalQuantity = order.OrderLines.Sum(l => l.Quantity)
            });
        }
    }

    // === Create user ===
    private void CreateUserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // === Read the username, password and if the user is admin === 
        var username = CreateUserUsername.Text;
        var password = CreateUserPassword.Text;
        var isAdmin = CreateUserIsAdmin.IsChecked ?? false;

        // === Stop if the username or the password is not written ===
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            // === Message ===
            Log("Username and password are required.");
            return;
        }

        // === Check if other has this username ===
        if (_accountService.UsernameExistsAsync(username).Result)
        {
            // === Message ===
            Log("Username already exists.");
            return;
        }

        // === Refister the new user in the database ===
        _accountService.NewAccountAsync(username, password, isAdmin).Wait();
        // === Message ===
        Log($"User '{username}' created (Admin: {isAdmin})");

        // === Clear the fields ===
        CreateUserUsername.Text = "";
        CreateUserPassword.Text = "";
        CreateUserIsAdmin.IsChecked = false;
    }

    // === Admin confirms order is completed ===
    private void OnConfirmOrderCompleted()
    {
        Task.Run(() =>
        {
            // === Add up how many components the order contains in total ===
            int a = DatabaseOrderLines.Where(x => x.ProductName == "Component A").Sum(x => x.Quantity);
            int b = DatabaseOrderLines.Where(x => x.ProductName == "Component B").Sum(x => x.Quantity);
            int c = DatabaseOrderLines.Where(x => x.ProductName == "Component C").Sum(x => x.Quantity);

            // === Send to robot === 
            RobotConnectionTest.RunOrder(a, b, c);
        });

        // === Message ===
        Log("Order sent to robot (one combined program)...");
    }

    // === When the robot has finished the order ===
    private async void OnConfirmOrderRemoveFromDatabase()
    {
        // === Find the latest order in the database === 
        var latestOrder = await _appDbContext.Orders
            .Include(o => o.OrderLines)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        // === Show message if there are no orders ===
        if (latestOrder == null)
        {
            Log("No order to remove.");
            return;
        }

        // === Remove all products in the order, deleting the order and removes the order from the database tab ===
        _appDbContext.OrderLines.RemoveRange(latestOrder.OrderLines);
        _appDbContext.Orders.Remove(latestOrder);
        await _appDbContext.SaveChangesAsync();

        // === Updates the display === 
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DatabaseOrderLines.Clear(); 
            LoadOrdersForUser(_currentUser);
        });
            
       // === Message ===
        Log("Latest order removed from database.");
    }



    private void RecreateDatabaseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = _appDbContext.Database.EnsureDeletedAsync();
        _ = EnsureDatabaseCreatedWithExampleDataAsync();
        Log("Database recreated.");
    }

    // === Recreate database button by deleting and creating a new database ===
    private void OnPlaceOrderClick(object? sender, RoutedEventArgs e)
    {
        Log("Opened new order page.");
        SelectedTabIndex = 3;
    }

    // === Clear log button ===
    private void ClearLogButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LogOutput.Text = "";
    }

    // === Place order button ===
    private void UpdateCanExecute()
    {
        PlaceOrderCommand?.RaiseCanExecuteChanged();
    }

    // === When clicking a button === 
    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("Process order button clicked.");
    }

    // === Messages in the log ===
    private void Log(string message)
    {
        LogOutput.Text += $"{DateTime.Now:HH:mm:ss} | {message}\n";
    }
}


// === Viewmodel ===
public class OrderViewModel
{
    public int OrderId { get; set; }
    public string CreatedAt { get; set; } = "";
    public int TotalQuantity { get; set; }
}

// === Updating the number displayed when a value is changed ===
public class ProductViewModel : INotifyPropertyChanged
{
    public string Name { get; }

    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }
    }

    // === Project view model ===
    public ProductViewModel(string name, int quantity = 0)
    {
        Name = name;
        Quantity = quantity;
    }

    // == Automatically updates the screen ===
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// === Connects the buttons to their actions ===
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

// === Actions for pressing the buttons so the program know which item to action on ===
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute == null || parameter is T;

    public void Execute(object? parameter)
    {
        if (parameter is T value)
            _execute(value);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty); 
    }

    }

