using InvestWallet.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InvestWallet.IntegrationTests;

/// <summary>Garante o comportamento de aplicar migrations na subida (README: "Decisões de arquitetura"), antes verificado só à mão.</summary>
[Collection(ApiCollection.Name)]
public class MigrationTests
{
    private readonly ApiFactory _factory;

    public MigrationTests(ApiFactory factory) => _factory = factory;

    private static IConfiguration ConfigWith(string? connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
        .Build();

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "mig_" + Guid.NewGuid().ToString("N")[..8];
        await using (var admin = new NpgsqlConnection(_factory.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }
        return new NpgsqlConnectionStringBuilder(_factory.ConnectionString) { Database = name }.ConnectionString;
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Num_banco_vazio_cria_o_esquema_e_rodar_de_novo_nao_faz_nada_nem_falha()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(ConfigWith(connectionString))
            .BuildServiceProvider();

        const string tables = "select count(*) from information_schema.tables where table_schema='public' and table_name in ('wallets','assets','transactions')";
        Assert.Equal(0, await ScalarAsync(connectionString, "select count(*) from information_schema.tables where table_schema='public'"));

        await provider.MigrateDatabaseAsync();

        Assert.Equal(3, await ScalarAsync(connectionString, tables));
        Assert.Equal(1, await ScalarAsync(connectionString, "select count(*) from \"__EFMigrationsHistory\""));

        await provider.MigrateDatabaseAsync(); // idempotente: segunda execução não lança nem duplica o histórico

        Assert.Equal(1, await ScalarAsync(connectionString, "select count(*) from \"__EFMigrationsHistory\""));
    }

    [Fact]
    public void Sem_connection_string_a_aplicacao_recusa_iniciar_com_mensagem_que_ensina_a_corrigir()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(ConfigWith(null)));

        Assert.Contains("ConnectionStrings__Default", ex.Message);
    }
}
