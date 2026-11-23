# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["Client/Client.csproj", "Client/"]
RUN dotnet restore "Client/Client.csproj"

# Copy source and publish
COPY Client/ Client/
WORKDIR /src/Client
RUN dotnet publish "Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV DBMS_HOST=db \
    DBMS_PORT=9090

ENTRYPOINT ["dotnet", "Client.dll"]
