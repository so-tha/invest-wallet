using System.Net;
using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// Violações de integridade do PostgreSQL devem virar ConflictException (409), nunca 500. Aqui provocamos as
/// violações DIRETAMENTE no DbContext, sem passar pelos serviços, que as evitariam com suas próprias checagens.
/// </summary>
[Collection(ApiCollection.Name)]
public class DatabaseErrorTranslationTests
{
    private readonly ApiFactory _factory;

    public DatabaseErrorTranslationTests(ApiFactory factory) => _factory = factory;

    private AppDbContext NewDbContext() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Violacao_do_indice_unico_do_ticker_vira_ConflictException_com_mensagem_clara()
    {
        var ticker = "U" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var db = NewDbContext();
        db.Assets.Add(new Asset(ticker, "Primeiro", AssetType.Stock));
        await db.SaveChangesAsync();

        var otherDb = NewDbContext();
        otherDb.Assets.Add(new Asset(ticker, "Duplicado", AssetType.Stock));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => otherDb.SaveChangesAsync());
        Assert.Contains("ticker", ex.Message);
    }

    [Fact]
    public async Task Violacao_de_chave_estrangeira_vira_ConflictException()
    {
        // Transação apontando para carteira e ativo que não existem (como se tivessem sido apagados no meio da operação)
        var db = NewDbContext();
        db.Transactions.Add(new Transaction(Guid.NewGuid(), Guid.NewGuid(), TransactionType.Buy, 1, 1, DateTime.UtcNow.AddDays(-1)));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        Assert.Contains("relacionados", ex.Message);
    }

    [Fact]
    public async Task Com_a_flag_MigrateOnStartup_ligada_a_aplicacao_sobe_normalmente_num_banco_ja_atualizado()
    {
        using var client = _factory
            .WithWebHostBuilder(b => b.UseSetting("Database:MigrateOnStartup", "true"))
            .CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/wallets")).StatusCode);
    }
}
