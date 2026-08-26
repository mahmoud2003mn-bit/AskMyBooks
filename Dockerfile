# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first so restore is cached separately from source changes
COPY src/AskMyBooks.Domain/AskMyBooks.Domain.csproj src/AskMyBooks.Domain/
COPY src/AskMyBooks.Services/AskMyBooks.Services.csproj src/AskMyBooks.Services/
COPY src/AskMyBooks.Data/AskMyBooks.Data.csproj src/AskMyBooks.Data/
COPY src/AskMyBooks.Web/AskMyBooks.Web.csproj src/AskMyBooks.Web/
RUN dotnet restore src/AskMyBooks.Web/AskMyBooks.Web.csproj

# Now copy everything else and publish
COPY src/ src/
WORKDIR /src/src/AskMyBooks.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# .NET 8's aspnet image listens on 8080 by default inside containers
EXPOSE 8080
ENTRYPOINT ["dotnet", "AskMyBooks.Web.dll"]
