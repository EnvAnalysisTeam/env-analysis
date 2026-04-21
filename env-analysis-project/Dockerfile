FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["env-analysis-project.csproj", "./"]
RUN dotnet restore "./env-analysis-project.csproj" -r linux-x64

COPY . .
RUN dotnet publish "./env-analysis-project.csproj" -c Release -r linux-x64 -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Cài LLVM OpenMP (tương thích ABI với Intel libiomp5)
RUN apt-get update && apt-get install -y libomp-dev && rm -rf /var/lib/apt/lists/*

# Symlink libomp.so.5 -> libiomp5.so (LLVM OpenMP thay thế Intel OpenMP)
RUN ln -s /usr/lib/x86_64-linux-gnu/libomp.so.5 /app/libiomp5.so

ENV LD_LIBRARY_PATH=/app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "env-analysis-project.dll"]
