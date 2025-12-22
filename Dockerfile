FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG SERVICE_NAME
WORKDIR /src

# Copy project file
COPY ["src/${SERVICE_NAME}/${SERVICE_NAME}.csproj", "src/${SERVICE_NAME}/"]
RUN dotnet restore "src/${SERVICE_NAME}/${SERVICE_NAME}.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/${SERVICE_NAME}"
RUN dotnet build "${SERVICE_NAME}.csproj" -c Release -o /app/build

FROM build AS publish
ARG SERVICE_NAME
RUN dotnet publish "${SERVICE_NAME}.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG SERVICE_NAME
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variable for the DLL name
ENV SERVICE_DLL="${SERVICE_NAME}.dll"

# Use JSON form for proper signal handling
ENTRYPOINT ["sh", "-c", "dotnet ${SERVICE_DLL}"]