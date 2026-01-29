FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY Served.MCP.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Environment variables
ENV SERVED_API_URL=https://apis.unifiedhq.ai
ENV SERVED_MCP_TRACING=false

ENTRYPOINT ["dotnet", "Served.MCP.dll"]
