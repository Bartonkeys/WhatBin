# Belfast Bins PWA

A Progressive Web App (PWA) for checking bin collection days in Belfast. Built with Angular and Ionic.

## Features

- 🗑️ **Bin Collection Lookup**: Enter your postcode to see when your bins are collected
- 💾 **Local Storage**: Your postcode is saved and automatically loaded on return visits
- 🎨 **Color-Coded Display**: Visual indicators for different bin types (Black, Blue, Brown)
- 📱 **Mobile-First Design**: Responsive Ionic UI optimized for mobile devices
- 🔌 **PWA Support**: Installable on mobile devices with offline manifest
- ⚡ **Fast & Lightweight**: Optimized bundle size for quick loading

## Live Demo

- **Frontend**: https://belfast-bin-collection-app-udfrk8i6.devinapps.com
- **Backend API**: https://app-isvwccpu.fly.dev/

## Tech Stack

### Frontend
- **Framework**: Angular 18+ with standalone components
- **UI Library**: Ionic 8+
- **Styling**: SCSS with Ionic theming
- **HTTP Client**: Angular HttpClient
- **Storage**: Browser localStorage API

### Backend
- **Framework**: FastAPI (Python)
- **Web Scraping**: Selenium + BeautifulSoup4
- **Deployment**: Fly.io

## Project Structure

```
belfast-bins/
├── src/
│   ├── app/
│   │   ├── home/              # Home page component
│   │   │   ├── home.page.ts
│   │   │   ├── home.page.html
│   │   │   └── home.page.scss
│   │   ├── services/          # Angular services
│   │   │   └── bin.service.ts # API & storage service
│   │   ├── app.component.ts
│   │   └── app.routes.ts
│   ├── assets/                # Static assets
│   ├── theme/                 # Ionic theme variables
│   ├── index.html
│   ├── main.ts                # App bootstrap
│   └── manifest.webmanifest   # PWA manifest
├── angular.json               # Angular configuration
├── capacitor.config.ts        # Capacitor configuration
├── ionic.config.json          # Ionic configuration
└── package.json
```

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- Angular CLI (`npm install -g @angular/cli`)
- Ionic CLI (`npm install -g @ionic/cli`)

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd belfast-bins
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

## Building for Production

Build the app for production:

```bash
npm run build
```

The build artifacts will be stored in the `www/` directory.

## API Integration

The app integrates with a FastAPI backend that scrapes bin collection data from the Belfast City Council website.

### API Endpoint

**POST** `/api/bin-lookup`

Request body:
```json
{
  "postcode": "BT14 7GP",
  "house_number": "420"  // optional
}
```

Response:
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

## Key Components

### BinService (`src/app/services/bin.service.ts`)

Handles API communication and local storage:
- `lookupBins(postcode, houseNumber)`: Fetches bin collection data
- `saveAddress(address)`: Saves address to localStorage
- `getSavedAddress()`: Retrieves saved address
- `clearSavedAddress()`: Clears saved address

### HomePage (`src/app/home/home.page.ts`)

Main application page:
- Form for postcode and house number input
- Displays bin collection results
- Auto-loads saved address on initialization
- Color-coded bin indicators

## PWA Features

The app includes a PWA manifest (`manifest.webmanifest`) with:
- App name and description
- Theme colors
- Display mode (standalone)
- App icons

Users can install the app on their mobile devices for a native-like experience.

## Styling

The app uses Ionic's theming system with custom SCSS:
- Color-coded bin badges (Black, Blue, Brown, Green, Purple)
- Responsive card layouts
- Loading states and error messages
- Mobile-optimized form inputs

## Local Storage

The app stores the user's postcode and house number in browser localStorage:
- Key: `belfast_bins_address`
- Value: JSON object with `postcode` and optional `houseNumber`
- Automatically loaded on app initialization

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari, Chrome Mobile)

## License

This project is for demonstration purposes.

## Acknowledgments

- Belfast City Council for bin collection data
- Ionic Framework for mobile UI components
- Angular team for the framework
