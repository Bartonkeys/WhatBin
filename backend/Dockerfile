FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install Chrome dependencies for Selenium
RUN apt-get update && apt-get install -y \
    wget \
    gnupg \
    ca-certificates \
    apt-transport-https \
    && wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | apt-key add - \
    && echo "deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update \
    && apt-get install -y google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BelfastBinsApi.csproj", "./"]
RUN dotnet restore "BelfastBinsApi.csproj"
COPY . .
RUN dotnet build "BelfastBinsApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BelfastBinsApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for Chrome
ENV CHROME_BIN=/usr/bin/google-chrome
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BelfastBinsApi.dll"]