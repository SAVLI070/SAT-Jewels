# Multi-stage Dockerfile for SAT-Jewels (.NET 8) deployment on AWS / Cloud Hosts
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SAT1.csproj", "./"]
RUN dotnet restore "SAT1.csproj"
COPY . .
RUN dotnet publish "SAT1.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SAT1.dll"]
