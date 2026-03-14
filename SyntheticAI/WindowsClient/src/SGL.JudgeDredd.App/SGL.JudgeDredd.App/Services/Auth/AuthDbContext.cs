using System.IO;
using Microsoft.EntityFrameworkCore;
namespace SGL.JudgeDredd.App.Services.Auth;

public class AuthDbContext : DbContext
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    private readonly string _dbPath;

    public AuthDbContext()
    {
        _dbPath = Path.Combine(AppContext.BaseDirectory, "data", "sgl-auth.db");
    }

    public AuthDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (dir != null) Directory.CreateDirectory(dir);
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email);
        });
    }
}
