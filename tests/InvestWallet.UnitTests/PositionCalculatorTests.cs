using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;
using InvestWallet.Domain.Positions;

namespace InvestWallet.UnitTests;

public class PositionCalculatorTests
{
    private static readonly Guid WalletId = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Transaction Buy(decimal qty, decimal price, int day = 0) =>
        new(WalletId, AssetId, TransactionType.Buy, qty, price, Day1.AddDays(day));

    private static Transaction Sell(decimal qty, decimal price, int day = 0) =>
        new(WalletId, AssetId, TransactionType.Sell, qty, price, Day1.AddDays(day));

    [Fact]
    public void Calcula_preco_medio_de_duas_compras()
    {
        var position = PositionCalculator.Calculate(AssetId, [Buy(10, 10, 0), Buy(10, 20, 1)]);

        Assert.Equal(20, position.Quantity);
        Assert.Equal(300, position.TotalCost);
        Assert.Equal(15, position.AveragePrice);
    }

    [Fact]
    public void Venda_reduz_quantidade_sem_alterar_preco_medio()
    {
        var position = PositionCalculator.Calculate(AssetId, [Buy(10, 10, 0), Buy(10, 20, 1), Sell(5, 99, 2)]);

        Assert.Equal(15, position.Quantity);
        Assert.Equal(15, position.AveragePrice); // o preço de venda (99) não interfere
        Assert.Equal(225, position.TotalCost);
    }

    [Fact]
    public void Vender_mais_do_que_possui_lanca_excecao()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PositionCalculator.Calculate(AssetId, [Buy(10, 10, 0), Sell(10.000001m, 10, 1)]));

        Assert.Contains("excede a posição disponível", ex.Message);
    }

    [Fact]
    public void Vender_antes_de_comprar_e_invalido_mesmo_se_o_total_fechar()
    {
        // Vendeu no dia 0 algo que só foi comprado no dia 1.
        Assert.Throws<DomainException>(() =>
            PositionCalculator.Calculate(AssetId, [Buy(10, 10, 1), Sell(10, 10, 0)]));
    }

    [Fact]
    public void A_ordem_da_lista_nao_importa_so_a_data_da_transacao()
    {
        // Lista "embaralhada": a venda aparece primeiro, mas aconteceu depois da compra.
        var position = PositionCalculator.Calculate(AssetId, [Sell(4, 10, 1), Buy(10, 10, 0)]);

        Assert.Equal(6, position.Quantity);
    }

    [Fact]
    public void Zerar_a_posicao_reinicia_o_preco_medio()
    {
        var position = PositionCalculator.Calculate(AssetId,
            [Buy(10, 10, 0), Sell(10, 50, 1), Buy(5, 30, 2)]);

        Assert.Equal(5, position.Quantity);
        Assert.Equal(30, position.AveragePrice); // não "herda" o custo da posição antiga
    }

    [Fact]
    public void Posicao_totalmente_vendida_tem_tudo_zerado()
    {
        var position = PositionCalculator.Calculate(AssetId, [Buy(3, 10, 0), Sell(3, 10, 1)]);

        Assert.Equal(0, position.Quantity);
        Assert.Equal(0, position.TotalCost);
        Assert.Equal(0, position.AveragePrice);
    }

    [Fact]
    public void Usa_decimal_sem_erro_de_ponto_flutuante()
    {
        var position = PositionCalculator.Calculate(AssetId, [Buy(0.1m, 1, 0), Buy(0.2m, 1, 1)]);

        Assert.Equal(0.3m, position.Quantity); // com double, 0.1 + 0.2 != 0.3
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    [InlineData(1.1234567, 10)]       // mais de 6 casas decimais
    [InlineData(1_000_000_000_000, 10)] // estoura numeric(18,6)
    public void Transacao_com_valores_invalidos_e_recusada(decimal qty, decimal price)
    {
        Assert.Throws<DomainException>(() => Buy(qty, price));
    }
}
