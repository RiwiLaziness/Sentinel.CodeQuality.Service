FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /build

COPY . .

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=builder /build/out .

ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "Sentinel.CodeQuality.Service.dll"]
