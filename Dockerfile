﻿FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["LoraGateway.csproj", "./"]
RUN dotnet restore "LoraGateway.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "LoraGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LoraGateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LoraGateway.dll"]
