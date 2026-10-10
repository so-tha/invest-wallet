using FluentValidation.TestHelper;
using InvestWallet.Application.Assets;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Enums;

namespace InvestWallet.UnitTests.Validators;

public class WalletRequestValidatorTests
{
    private readonly WalletRequestValidator _validator = new();

    [Fact]
    public void Requisicao_valida_nao_tem_erros()
    {
        _validator.TestValidate(new WalletRequest("Minha carteira", "descrição"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_ausente_ou_em_branco_e_erro(string? name)
    {
        _validator.TestValidate(new WalletRequest(name!, null))
            .ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("O nome da carteira é obrigatório.");
    }

    [Fact]
    public void Limites_de_tamanho_sao_respeitados_no_valor_exato()
    {
        _validator.TestValidate(new WalletRequest(new string('a', 100), new string('b', 300)))
            .ShouldNotHaveAnyValidationErrors();

        var result = _validator.TestValidate(new WalletRequest(new string('a', 101), new string('b', 301)));
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }
}

public class AssetRequestValidatorTests
{
    private readonly AssetRequestValidator _validator = new();

    [Fact]
    public void Requisicao_valida_nao_tem_erros()
    {
        _validator.TestValidate(new AssetRequest("PETR4", "Petrobras", AssetType.Stock))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Todos_os_campos_invalidos_geram_um_erro_cada()
    {
        var result = _validator.TestValidate(new AssetRequest("", "", (AssetType)99));

        result.ShouldHaveValidationErrorFor(x => x.Ticker);
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }
}
