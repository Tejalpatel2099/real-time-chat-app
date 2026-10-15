# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ChatApp.Api/ChatApp.Api.csproj src/ChatApp.Api/
RUN dotnet restore src/ChatApp.Api/ChatApp.Api.csproj
COPY src/ChatApp.Api/ src/ChatApp.Api/
RUN dotnet publish src/ChatApp.Api/ChatApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChatApp.Api.dll"]
