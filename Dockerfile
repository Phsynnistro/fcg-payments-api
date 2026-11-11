# =============================================================================
#  Fcg.Payments.Api — Dockerfile multi-stage otimizado para produção
#   build  : SDK completo (~800 MB) — compila e publica
#   test   : opcional (docker build --target test .) — roda os testes no pipeline
#   final  : runtime ASP.NET "chiseled" (~110 MB): sem shell, sem gerenciador de
#            pacotes, usuário não-root por padrão → superfície de ataque mínima
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1) Só o que afeta o restore. Enquanto csproj/props não mudarem, esta camada (com os pacotes
#    baixados em /root/.nuget/packages) vem do cache e o restore não roda a cada mudança de código.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Fcg.Payments.Api/Fcg.Payments.Api.csproj src/Fcg.Payments.Api/
RUN dotnet restore src/Fcg.Payments.Api/Fcg.Payments.Api.csproj

# 2) Código-fonte + publish (sem apphost: a imagem final já tem o runtime "dotnet").
COPY src/ src/
RUN dotnet publish src/Fcg.Payments.Api/Fcg.Payments.Api.csproj \
      -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# ---- Estágio opcional de testes ---------------------------------------------
FROM build AS test
COPY tests/ tests/
RUN dotnet test tests/Fcg.Payments.UnitTests/Fcg.Payments.UnitTests.csproj -c Release

# ---- Imagem final -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app/publish .
# UID 1654 ("app"), definido pela própria imagem. O Kubernetes reforça com runAsNonRoot.
USER $APP_UID
ENTRYPOINT ["dotnet", "Fcg.Payments.Api.dll"]
