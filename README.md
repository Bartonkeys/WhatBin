# WhatBin - Belfast Bin Collection PWA

A Progressive Web App (PWA) for checking bin collection days in Belfast. Built with Angular, Ionic, and ASP.NET Core.

## 🌐 Live Demo

- **Frontend PWA**: https://belfast-bin-collection-app-udfrk8i6.devinapps.com
- **Backend API**: https://app-isvwccpu.fly.dev/

## ✨ Features

- 🗑️ **Bin Collection Lookup**: Enter your postcode to see when your bins are collected
- 💾 **Local Storage**: Your postcode is saved and automatically loaded on return visits
- 🎨 **Color-Coded Display**: Visual indicators for different bin types (Black, Blue, Brown)
- 📱 **Mobile-First Design**: Responsive Ionic UI optimized for mobile devices
- 🔌 **PWA Support**: Installable on mobile devices with offline manifest
- ⚡ **Fast & Lightweight**: Optimized bundle size for quick loading
- 🔄 **Auto-Refresh**: Automatically loads saved address on app start

## 🏗️ Architecture

### Frontend (`/frontend`)
- **Framework**: Angular 18+ with standalone components
- **UI Library**: Ionic 8+
- **Styling**: SCSS with Ionic theming
- **HTTP Client**: Angular HttpClient
- **Storage**: Browser localStorage API
- **PWA**: Web App Manifest for installability

### Backend (`/backend`)
- **Framework**: ASP.NET Core 8.0 (C#)
- **Web Scraping**: Selenium WebDriver + HtmlAgilityPack
- **Browser Automation**: Chrome WebDriver
- **Deployment**: Cloud-ready (Azure, AWS, Docker)
- **CORS**: Configured for cross-origin requests

## 📁 Project Structure

```
WhatBin/
├── frontend/                  # Angular + Ionic PWA
│   ├── src/
│   │   ├── app/
│   │   │   ├── home/         # Home page component
│   │   │   ├── services/     # Angular services
│   │   │   ├── app.component.ts
│   │   │   └── app.routes.ts
│   │   ├── assets/           # Static assets
│   │   ├── theme/            # Ionic theme variables
│   │   ├── index.html
│   │   ├── main.ts
│   │   └── manifest.webmanifest
│   ├── angular.json
│   ├── capacitor.config.ts
│   ├── ionic.config.json
│   └── package.json
│
└── backend/                   # ASP.NET Core backend
    ├── Controllers/
    │   └── BinLookupController.cs  # API endpoints
    ├── Models/               # Data models
    ├── Services/
    │   └── BinScraperService.cs    # Web scraping logic
    ├── Program.cs            # App configuration
    └── BelfastBinsApi.csproj # Project file
```

## 🚀 Getting Started

### Prerequisites

- **Frontend**:
  - Node.js 18+ and npm
  - Angular CLI: `npm install -g @angular/cli`
  - Ionic CLI: `npm install -g @ionic/cli`

- **Backend**:
  - .NET SDK 8.0+
  - Chrome/Chromium browser (for Selenium)

### Frontend Setup

1. Navigate to the frontend directory:
```bash
cd frontend
```

2. Install dependencies:
```bash
npm install
```

3. Start the development server:
```bash
npm start
# or
ionic serve
```

4. Open your browser to `http://localhost:4200`

### Backend Setup

1. Navigate to the backend directory:
```bash
cd backend
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Start the development server:
```bash
dotnet run
```

4. The API will be available at `http://localhost:5000` (or `https://localhost:5001` for HTTPS)

## 🔧 Building for Production

### Frontend

Build the PWA:
```bash
cd frontend
npm run build
```

The build artifacts will be in the `www/` directory.

### Backend

Build the backend:
```bash
cd backend
dotnet build
```

The backend can be deployed to various platforms:
- **Azure App Service**: `az webapp up`
- **Docker**: Build and deploy using the included Dockerfile
- **AWS**: Deploy using Elastic Beanstalk or ECS
- **Self-hosted**: Run with `dotnet run` or as a Windows Service

## 📡 API Documentation

### Endpoint: Bin Lookup

**POST** `/api/bin-lookup`

Retrieves bin collection information for a given postcode.

**Request Body:**
```json
{
  "postcode": "BT14 7GP",
  "house_number": "420"  // optional
}
```

**Response:**
```json
{
  "address": "Sample Address, BT14 7GP",
  "collections": [
    {
      "bin_type": "General Waste (Black Bin)",
      "color": "Black",
      "next_collection": "Monday, 27 October 2025"
    },
    {
      "bin_type": "Recycling (Blue Bin)",
      "color": "Blue",
      "next_collection": "Thursday, 30 October 2025"
    },
    {
      "bin_type": "Garden Waste (Brown Bin)",
      "color": "Brown",
      "next_collection": "Thursday, 30 October 2025"
    }
  ],
  "next_collection_color": "Black"
}
```

**Error Response:**
```json
{
  "detail": "Error message"
}
```

## 🎨 Key Components

### Frontend

#### BinService (`frontend/src/app/services/bin.service.ts`)
Handles API communication and local storage:
- `lookupBins(postcode, houseNumber)`: Fetches bin collection data from backend
- `saveAddress(address)`: Saves address to localStorage
- `getSavedAddress()`: Retrieves saved address from localStorage
- `clearSavedAddress()`: Clears saved address from localStorage

#### HomePage (`frontend/src/app/home/home.page.ts`)
Main application page:
- Form for postcode and house number input
- Displays bin collection results with color-coded indicators
- Auto-loads saved address on initialization
- Loading states and error handling

### Backend

#### BinScraperService (`backend/Services/BinScraperService.cs`)
Web scraping service with:
- `ScrapeBinData()`: Scrapes Belfast City Council website using Selenium
- `GetMockBinData()`: Fallback mock data for development
- `SetupDriver()`: Configures Chrome WebDriver with headless options

#### BinLookupController (`backend/Controllers/BinLookupController.cs`)
API controller with:
- `GET /api/healthz`: Health check endpoint
- `POST /api/bin-lookup`: Bin collection lookup endpoint
- CORS configured for cross-origin requests

## 💾 Data Storage

### Local Storage
The app stores user data in browser localStorage:
- **Key**: `belfast_bins_address`
- **Value**: JSON object with `postcode` and optional `houseNumber`
- **Behavior**: Automatically loaded on app initialization

### Backend Data Source
The backend scrapes data from:
- **URL**: https://online.belfastcity.gov.uk/find-bin-collection-day/Default.aspx
- **Method**: Selenium WebDriver with Chrome
- **Fallback**: Mock data when scraping fails (development mode)

## 🎨 Styling & Theming

The app uses Ionic's theming system with custom SCSS:

### Color-Coded Bins
- **Black Bin**: `#2d2d2d` - General Waste
- **Blue Bin**: `#3880ff` - Recycling
- **Brown Bin**: `#8b4513` - Garden Waste
- **Green Bin**: `#2dd36f` - (if applicable)
- **Purple Bin**: `#9333ea` - (if applicable)

### UI Components
- Responsive card layouts
- Loading spinners with messages
- Error message displays
- Color-coded circular indicators
- Prominent "Next Collection" badge

## 🌐 Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- iOS Safari 14+
- Chrome Mobile

## 🔐 Environment Variables

### Frontend
The frontend uses the deployed backend URL hardcoded in the service:
- **API URL**: `https://app-isvwccpu.fly.dev/api`

To change the backend URL, update `frontend/src/app/services/bin.service.ts`:
```typescript
private apiUrl = 'https://your-backend-url.com/api';
```

### Backend
No environment variables required. The backend uses:
- Mock data fallback for development
- Selenium with Chrome WebDriver for production scraping

## 🚢 Deployment

### Frontend Deployment
The frontend is deployed as a static PWA:
1. Build: `npm run build`
2. Deploy the `www/` directory to any static hosting service
3. Current deployment: https://belfast-bin-collection-app-udfrk8i6.devinapps.com

### Backend Deployment
The backend can be deployed to various platforms:
1. **Azure App Service**: Use `az webapp up` or deploy from Visual Studio
2. **Docker**: Build and deploy using containerization
3. **AWS**: Deploy using Elastic Beanstalk or ECS
4. **Self-hosted**: Run with `dotnet run` or as a Windows Service
5. Previous deployment: https://app-isvwccpu.fly.dev/ (FastAPI version)

## 🧪 Testing

### Frontend Testing
```bash
cd frontend
npm test
```

### Backend Testing
Test the API endpoint:
```bash
curl -X POST http://localhost:5000/api/bin-lookup \
  -H "Content-Type: application/json" \
  -d '{"postcode": "BT14 7GP"}'
```

## 📝 Development Notes

### Backend Scraping
- The backend uses Selenium WebDriver with Chrome in headless mode
- HtmlAgilityPack is used for HTML parsing
- Chrome WebDriver is required for production scraping
- Mock data is used as fallback when Chrome is unavailable or scraping fails
- The scraper handles ASP.NET form submissions and viewstate

### PWA Features
- Web App Manifest configured for installability
- Theme colors match Ionic primary color
- Standalone display mode for native-like experience
- Icons configured for home screen installation

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -m 'Add some feature'`
4. Push to the branch: `git push origin feature/your-feature`
5. Open a Pull Request

## 📄 License

This project is for demonstration purposes.

## 🙏 Acknowledgments

- Belfast City Council for bin collection data
- Ionic Framework for mobile UI components
- Angular team for the framework
- ASP.NET Core team for the backend framework

## 📞 Support

For issues or questions, please open an issue on GitHub.

---

**Built with ❤️ for Belfast residents**
