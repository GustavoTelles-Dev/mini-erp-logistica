# Imagem da MiniErpApi para o Railway (ou qualquer servidor com Docker).
# Duas etapas: a primeira compila com o SDK completo; a segunda roda so com o runtime,
# o que deixa a imagem final bem menor e sem ferramentas de compilacao.

# ---------- 1) Compilacao ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacao
WORKDIR /src

# Restaura os pacotes antes de copiar o codigo: se so o codigo mudar, o Docker reaproveita essa etapa
COPY MiniErpApi/MiniErpApi.csproj MiniErpApi/
RUN dotnet restore MiniErpApi/MiniErpApi.csproj

COPY MiniErpApi/ MiniErpApi/
RUN dotnet publish MiniErpApi/MiniErpApi.csproj -c Release -o /app --no-restore

# ---------- 2) Execucao ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilacao /app .

ENV ASPNETCORE_ENVIRONMENT=Production

# Roda com o usuario sem privilegios que ja vem na imagem oficial (nunca como root)
USER app

# O Railway informa a porta na variavel PORT; fora dele, usa a 8080
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet MiniErpApi.dll"]
