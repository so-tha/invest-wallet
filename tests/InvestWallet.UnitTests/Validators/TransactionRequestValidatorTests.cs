using FluentValidation.TestHelper;
using InvestWallet.Application.Transactions;
using InvestWallet.Domain.Enums;
using InvestWallet.UnitTests.Support;

namespace InvestWallet.UnitTests.Validators;

public class TransactionRequestValidatorTests
{
    private readonly TransactionRequestValidator _validator = new(TestData.Clock());

    private static TransactionRequest Valid() =>
        new(Guid.NewGuid(), TransactionType.Buy, 10, 25.5m, TestData.Now.AddDays(-1));

    [Fact]
    public void Requisicao_valida_nao_tem_erros() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantidade_deve_ser_positiva(double value) =>
        _validator.TestValidate(Valid() with { Quantity = (decimal)value })
            .ShouldHaveValidationErrorFor(x => x.Quantity)
            .WithErrorMessage("A quantidade deve ser maior que zero.");

    [Fact]
    public void Aceita_ate_6_casas_decimais_e_recusa_7()
    {
        _validator.TestValidate(Valid() with { Quantity = 1.123456m }).ShouldNotHaveValidationErrorFor(x => x.Quantity);
        _validator.TestValidate(Valid() with { Quantity = 1.1234567m }).ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Recusa_valor_com_mais_de_12_digitos_inteiros()
    {
        _validator.TestValidate(Valid() with { UnitPrice = 999_999_999_999m }).ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
        _validator.TestValidate(Valid() with { UnitPrice = 1_000_000_000_000m }).ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void Preco_negativo_gera_apenas_um_erro_por_causa_do_Cascade_Stop()
    {
        var result = _validator.TestValidate(Valid() with { UnitPrice = -1.1234567m });

        Assert.Single(result.Errors.Where(e => e.PropertyName == nameof(TransactionRequest.UnitPrice)));
    }

    [Fact]
    public void Ativo_vazio_e_erro() =>
        _validator.TestValidate(Valid() with { AssetId = Guid.Empty }).ShouldHaveValidationErrorFor(x => x.AssetId);

    [Fact]
    public void Tipo_inexistente_e_erro() =>
        _validator.TestValidate(Valid() with { Type = (TransactionType)99 }).ShouldHaveValidationErrorFor(x => x.Type);

    [Fact]
    public void Data_ausente_e_erro() =>
        _validator.TestValidate(Valid() with { ExecutedAt = default }).ShouldHaveValidationErrorFor(x => x.ExecutedAt);

    [Fact]
    public void Data_no_futuro_e_erro_e_exatamente_agora_ainda_vale()
    {
        // O relógio é falso e fixo: o teste é determinístico, sem depender da hora real.
        _validator.TestValidate(Valid() with { ExecutedAt = TestData.Now }).ShouldNotHaveValidationErrorFor(x => x.ExecutedAt);

        _validator.TestValidate(Valid() with { ExecutedAt = TestData.Now.AddSeconds(1) })
            .ShouldHaveValidationErrorFor(x => x.ExecutedAt)
            .WithErrorMessage("A data da transação não pode estar no futuro.");
    }

    [Fact]
    public void Data_futura_so_e_aceita_depois_que_o_relogio_avanca()
    {
        var clock = TestData.Clock();
        var validator = new TransactionRequestValidator(clock);
        var request = Valid() with { ExecutedAt = TestData.Now.AddDays(1) };

        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ExecutedAt);

        clock.Advance(TimeSpan.FromDays(2));

        validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.ExecutedAt);
    }
}
