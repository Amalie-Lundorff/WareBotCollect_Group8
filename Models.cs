using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

// <Summary>
    //Koderne er et simpelt login system med database. 
    //Man kan oprette brugere, tjekke om et brugernavn findes, validere login ved at hashe password
    //Yderligere er der datamodeller til ordre og ordrelinjer

namespace SystemLogin;

// Service-lag til håndtering af konti 
public class AccountService(AppDbContext db, PasswordHasher hasher)
{
    //Opretter en ny bruger i databasen 
        // Password gemmes ikke i klar tekst, da vi hasher koden. 
        // Password hashes med PBKDF2 + SHA256
    
    public async Task NewAccountAsync(string username, string password, bool isAdmin = false)
    {
        var (salt, saltedPasswordHash) = hasher.Hash(password);
        db.Add(new Account
        {
            Username = username,
            Salt = salt,
            SaltedPasswordHash = saltedPasswordHash,
            isAdmin = isAdmin
        });
        //Gemmer ændringer i databasen 
        await db.SaveChangesAsync();
    }

    //Tjekker om et brugernavn allerede eksistere i databasen 
    // returnere true hvis ikke brugeren findes 
    public Task<bool> UsernameExistsAsync(string username)
    {
        return db.Accounts.AnyAsync(a => a.Username == username);
    }

    // Checker om login-oplysninger er korrekte 
        // Finder brugeren i databasen 
        // Hasher indtastet passwrod 
        // Sammenligner hash med den gemte hash 
    public async Task<bool> CredentialsCorrectAsync(string username, string password)
    {
        var account = await db.Accounts.FirstAsync(a => a.Username == username);
        return hasher.PasswordCorrect(password, account.Salt, account.SaltedPasswordHash);
    }

    //Returner om brugeren er admin 
    public Task<bool> UserIsAdminAsync(string username)
    {
        return db.Accounts.Where(a => a.Username == username).Select(a => a.isAdmin).FirstAsync();
    }

    // Henter en konto fra databasen baseret på brugernavn 
    public Task<Account> GetAccountAsync(string username)
    {
        return db.Accounts.FirstAsync(a => a.Username == username);
    }
}

//Ansvarlig for hashing af passwords 
public class PasswordHasher(
    int saltLength = 128 / 8,
    int hashIterations = 600_000
)
{
    // Tjekker om et indtatset password matcher det gemte hashet passwrod 
    public bool PasswordCorrect(string password, byte[] salt, byte[] saltedPasswordHash)
    {
        return CryptographicOperations.FixedTimeEquals(Hash(salt, password), saltedPasswordHash);
    }

    // Hash-funktion der kombinerer password + salt via PBKDF2
    private byte[] Hash(byte[] salt, string password)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            hashIterations,
            HashAlgorithmName.SHA256,
            256 / 8
        );
    }

    // Opretter nyt random salt 
    // Bruges ved oprettelse af nye accounts 
    public (byte[] Salt, byte[] Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(saltLength);
        return (salt, Hash(salt, password));
    }
}

// Databasekontekst 
// Bruger SQLite databasefil 
public class AppDbContext(string dbPath = "../../../database.sqlite") : DbContext
{
    // DbSet = tabel i databsen 
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}

// ========== MODELS ==========
// Username er primær nøgle 
public class Account
{
    [Key]
    public string Username { get; set; }

    //Password lagres sikkert 
    public byte[] Salt { get; set; }
    public byte[] SaltedPasswordHash { get; set; }
    public bool isAdmin { get; set; }
}

//Order-model 
public class Order
{
    [Key]
    public int Id { get; set; }

    //Tidspunkt for oprettelse 
    public DateTime CreatedAt { get; set; }
    // Status for ordre 
    public string Status { get; set; } = "Pending";

    // Hvilken bruger der ejer ordren 
    public string AccountUsername { get; set; }
    // En ordre kan have mange ordrelinjer 
    public List<OrderLine> OrderLines { get; set; } = new();
}

//OrderLine-model - En linje i en ordre 
public class OrderLine
{
    public int Id { get; set; }

    public string ProductName { get; set; } = "";  // ← Denne linje mangler!
    public int Quantity { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }
}


