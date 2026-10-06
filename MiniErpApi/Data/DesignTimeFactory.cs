using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Usado so pelos comandos "dotnet ef" (ex.: migrations add).
// Le a connection string dos user-secrets ou de variavel de ambiente;
// se nao achar, usa um endereco ficticio (criar migration nao precisa de banco).
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string conexao = config.GetConnectionString("Supabase") ?? "Host=localhost;Database=design";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexao)
            .Options;

        return new AppDbContext(options, new SessaoAtual());
    }
}
