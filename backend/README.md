# Belfast Bins API - ASP.NET Core Backend

ASP.NET Core Web API backend for the Belfast Bins PWA. Scrapes bin collection data from the Belfast City Council website using Selenium.

## 🚀 Features

- **Web Scraping**: Selenium WebDriver with Chrome for scraping Belfast City Council website
- **Mock Data Fallback**: Automatic fallback to mock data when scraping fails (development mode)
- **CORS Enabled**: Configured for cross-origin requests from frontend
- **RESTful API**: Clean API endpoints for bin collection lookup
- **Health Check**: Health check endpoint for monitoring

## 🏗️ Architecture

### Technology Stack
- **Framework**: ASP.NET Core 8.0
- **Web Scraping**: Selenium WebDriver 4.38.0
- **HTML Parsing**: HtmlAgilityPack 1.12.4
- **Browser**: Chrome/Chromium with ChromeDriver

### Project Structure
```
backend/
├── Controllers/
│   └── BinLookupController.cs    # API endpoints
├── Models/
│   ├── BinCollection.cs          # Bin collection model
│   ├── BinLookupRequest.cs       # Request model
│   └── BinLookupResponse.cs      # Response model
├── Services/
│   └── BinScraperService.cs      # Web scraping logic
├── Program.cs                     # App configuration
└── BelfastBinsApi.csproj         # Project file
```

## 📋 Prerequisites

- **.NET SDK 8.0+**: [Download here](https://dotnet.microsoft.com/download)
- **Chrome/Chromium**: Required for Selenium web scraping
- **ChromeDriver**: Automatically managed by Selenium.WebDriver.ChromeDriver package

## 🔧 Installation

1. **Navigate to backend directory**:
```bash
cd backend
```

2. **Restore dependencies**:
```bash
dotnet restore
```

3. **Build the project**:
```bash
dotnet build
```

## 🚀 Running the Backend

### Development Mode

Run the backend with hot reload:
```bash
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### Production Mode

Build and run in production:
```bash
dotnet build --configuration Release
dotnet run --configuration Release
```

## 📡 API Endpoints

### Health Check

**GET** `/api/healthz`

Check if the API is running.

**Response:**
```json
{
  "status": "ok"
}
```

### Bin Collection Lookup

**POST** `/api/bin-lookup`

Retrieve bin collection information for a Belfast postcode.

**Request Body:**
```json
{
  "postcode": "BT14 7GP",
  "houseNumber": "420"  // optional
}
```

**Response:**
```json
{
  "address": "Sample Address, BT14 7GP",
  "collections": [
    {
      "binType": "General Waste (Black Bin)",
      "color": "Black",
      "nextCollection": "Monday, 27 October 2025"
    },
    {
      "binType": "Recycling (Blue Bin)",
      "color": "Blue",
      "nextCollection": "Thursday, 30 October 2025"
    },
    {
      "binType": "Garden Waste (Brown Bin)",
      "color": "Brown",
      "nextCollection": "Thursday, 30 October 2025"
    }
  ],
  "nextCollectionColor": "Black"
}
```

**Error Response:**
```json
{
  "detail": "Error message"
}
```

## 🔍 How It Works

### Web Scraping Process

1. **Initialize Chrome WebDriver** with headless mode
2. **Navigate** to Belfast City Council bin lookup page
3. **Select postcode search** option
4. **Enter postcode** and submit form
5. **Select address** from dropdown (with optional house number filtering)
6. **Parse results** from the results page using HtmlAgilityPack
7. **Extract bin collection data** including bin type, color, and next collection date
8. **Return structured JSON** response

### Mock Data Fallback

When web scraping fails (e.g., Chrome not available, network issues), the API automatically falls back to mock data:
- Generates realistic bin collection dates
- Returns sample data for all bin types
- Logs the failure for debugging

### CORS Configuration

The backend is configured to allow all origins for development:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

## 🧪 Testing

### Test with curl

Test the health check endpoint:
```bash
curl http://localhost:5000/api/healthz
```

Test the bin lookup endpoint:
```bash
curl -X POST http://localhost:5000/api/bin-lookup \
  -H "Content-Type: application/json" \
  -d '{"postcode": "BT14 7GP"}'
```

Test with house number:
```bash
curl -X POST http://localhost:5000/api/bin-lookup \
  -H "Content-Type: application/json" \
  -d '{"postcode": "BT14 7GP", "houseNumber": "420"}'
```

## 🐛 Troubleshooting

### Chrome/ChromeDriver Issues

If you encounter Chrome-related errors:

1. **Install Chrome/Chromium**:
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y chromium-browser

# Or Google Chrome
wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
sudo dpkg -i google-chrome-stable_current_amd64.deb
```

2. **Set Chrome binary location** (if needed):
```bash
export CHROME_BIN=/usr/bin/google-chrome
```

### Build Errors

If you encounter build errors:

1. **Clean and rebuild**:
```bash
dotnet clean
dotnet restore
dotnet build
```

2. **Check .NET SDK version**:
```bash
dotnet --version
```

### Scraping Failures

If scraping consistently fails:
- Check internet connectivity
- Verify Belfast City Council website is accessible
- Check Chrome/ChromeDriver compatibility
- Review logs for specific error messages

The API will automatically fall back to mock data, so the frontend will continue to work.

## 🔐 Environment Variables

### Optional Configuration

- **CHROME_BIN**: Path to Chrome binary (default: `/usr/bin/google-chrome`)
- **ASPNETCORE_URLS**: Override default URLs (default: `http://localhost:5000`)
- **ASPNETCORE_ENVIRONMENT**: Set environment (Development/Production)

Example:
```bash
export CHROME_BIN=/usr/bin/chromium-browser
export ASPNETCORE_URLS="http://localhost:8000"
dotnet run
```

## 📦 NuGet Packages

- **Selenium.WebDriver** (4.38.0): Browser automation
- **Selenium.WebDriver.ChromeDriver** (141.0.7390.12200): ChromeDriver binaries
- **Selenium.Support** (4.38.0): Selenium support classes (SelectElement, etc.)
- **HtmlAgilityPack** (1.12.4): HTML parsing

## 🚢 Deployment

### Docker Deployment

Create a `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BelfastBinsApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BelfastBinsApi.dll"]
```

Build and run:
```bash
docker build -t belfast-bins-api .
docker run -p 8000:80 belfast-bins-api
```

### Cloud Deployment

The backend can be deployed to:
- **Azure App Service**: Native .NET support
- **AWS Elastic Beanstalk**: .NET Core support
- **Google Cloud Run**: Container-based deployment
- **Heroku**: Using Docker buildpack

## 📝 Development Notes

### Code Style
- Uses C# 12 features and nullable reference types
- Follows ASP.NET Core conventions
- Dependency injection for services
- Async/await for I/O operations

### Logging
The application uses ASP.NET Core's built-in logging:
```csharp
_logger.LogWarning("Scraping failed, using mock data");
_logger.LogError(ex, "Error processing bin lookup request");
```

### Error Handling
- Try-catch blocks for scraping failures
- Automatic fallback to mock data
- Proper HTTP status codes (400, 500)
- Detailed error messages in responses

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

This project is for demonstration purposes.

## 🙏 Acknowledgments

- Belfast City Council for bin collection data
- Selenium WebDriver for browser automation
- ASP.NET Core team for the framework

---

**Built with ASP.NET Core 8.0 for Belfast residents**
