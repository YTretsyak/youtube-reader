FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY youtube-reader.sln .
COPY Directory.Build.props .
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Contracts/Contracts.csproj src/Contracts/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/
COPY src/TranscriptService/TranscriptService.csproj src/TranscriptService/
COPY tests/Domain.Tests/Domain.Tests.csproj tests/Domain.Tests/
COPY tests/Application.Tests/Application.Tests.csproj tests/Application.Tests/
COPY tests/Infrastructure.Tests/Infrastructure.Tests.csproj tests/Infrastructure.Tests/
COPY tests/TranscriptService.Tests/TranscriptService.Tests.csproj tests/TranscriptService.Tests/
COPY tests/Api.Tests/Api.Tests.csproj tests/Api.Tests/
RUN dotnet restore src/Api/Api.csproj

COPY . .
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
