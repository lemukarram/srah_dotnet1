# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (layer cache friendly)
COPY ["SarhSummarizer/SarhSummarizer.csproj", "SarhSummarizer/"]
RUN dotnet restore "SarhSummarizer/SarhSummarizer.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/SarhSummarizer"
RUN dotnet build "SarhSummarizer.csproj" -c Release -o /app/build

# ============================================================
# Stage 2: Publish
# ============================================================
FROM build AS publish
RUN dotnet publish "SarhSummarizer.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ============================================================
# Stage 3: Runtime (smallest possible image)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

# Copy published output
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

USER appuser

# ASP.NET Core listens on 8080 by default in containers (not 5000)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "SarhSummarizer.dll"]
