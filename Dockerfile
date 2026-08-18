# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartReviewService.sln Directory.Build.props nuget.config ./
COPY packages/ packages/
COPY src/Api/Kart.Review.Api.csproj src/Api/
COPY src/Application/Kart.Review.Application.csproj src/Application/
COPY src/Domain/Kart.Review.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Review.Infrastructure.csproj src/Infrastructure/
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore src/Api/Kart.Review.Api.csproj

COPY src/ src/
COPY contracts/ contracts/
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish src/Api/Kart.Review.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kart.Review.Api.dll"]
