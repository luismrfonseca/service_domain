# Stage 1: Build C# projects
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ServiceDomain.Api/ServiceDomain.Api.csproj", "src/ServiceDomain.Api/"]
COPY ["src/ServiceDomain.Core/ServiceDomain.Core.csproj", "src/ServiceDomain.Core/"]
RUN dotnet restore "src/ServiceDomain.Api/ServiceDomain.Api.csproj"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/src/ServiceDomain.Api"
RUN dotnet publish "ServiceDomain.Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Environment variable to expose dynamic port on Railway
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ServiceDomain.Api.dll"]
