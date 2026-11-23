# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["Database/Database.csproj", "Database/"]
RUN dotnet restore "Database/Database.csproj"

# Copy source and publish
COPY Database/ Database/
WORKDIR /src/Database
RUN dotnet publish "Database.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Persist database files
VOLUME ["/app/databases_list"]

ENV DBMS_HOST=0.0.0.0 \
    DBMS_PORT=9090

EXPOSE 9090
ENTRYPOINT ["dotnet", "Database.dll"]
