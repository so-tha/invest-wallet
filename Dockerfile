# syntax=docker/dockerfile:1

# ---------- Estágio 1: build (imagem grande, com o SDK; descartada no final) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar SÓ os .csproj antes do restante do código: enquanto as dependências não mudarem, o Docker reaproveita
# o cache desta camada e o `dotnet restore` (lento, baixa pacotes) não roda de novo a cada alteração de código.
COPY src/InvestWallet.Domain/InvestWallet.Domain.csproj                 src/InvestWallet.Domain/
COPY src/InvestWallet.Application/InvestWallet.Application.csproj       src/InvestWallet.Application/
COPY src/InvestWallet.Infrastructure/InvestWallet.Infrastructure.csproj src/InvestWallet.Infrastructure/
COPY src/InvestWallet.Api/InvestWallet.Api.csproj                       src/InvestWallet.Api/
RUN dotnet restore src/InvestWallet.Api/InvestWallet.Api.csproj

COPY src/ src/
RUN dotnet publish src/InvestWallet.Api/InvestWallet.Api.csproj \
      -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---------- Estágio 2: runtime (imagem pequena, só o necessário para executar) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Usuário sem privilégios (já existe nas imagens .NET 8): se a aplicação for comprometida,
# o invasor não é root dentro do container.
USER $APP_UID

# Imagens .NET 8 escutam na 8080 (não privilegiada) por padrão
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "InvestWallet.Api.dll"]
