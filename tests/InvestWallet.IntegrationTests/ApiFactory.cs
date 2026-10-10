using InvestWallet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// Sobe um PostgreSQL descartável em Docker (Testcontainers) e a API real apontando para ele.
/// Um único container é compartilhado por todos os testes (iniciar leva alguns segundos); por isso cada
/// teste cria seus próprios dados com nomes/tickers únicos, em vez de limpar o banco entre testes.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Mesma versão do PostgreSQL usada no docker-compose.yml: o teste roda no banco que roda em produção
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // A connection string precisa existir ANTES de a aplicação ser construída (o Program a lê na inicialização)
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());

        // Acessar Services constrói a aplicação; aplicamos as migrations reais, as mesmas de produção
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>Todas as classes de teste desta coleção compartilham a mesma instância de <see cref="ApiFactory"/>.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
