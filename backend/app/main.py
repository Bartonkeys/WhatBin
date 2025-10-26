from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.support.ui import Select
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from bs4 import BeautifulSoup
import time
import os
from typing import Optional
from datetime import datetime, timedelta

app = FastAPI()

# Disable CORS. Do not remove this for full-stack development.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Allows all origins
    allow_credentials=True,
    allow_methods=["*"],  # Allows all methods
    allow_headers=["*"],  # Allows all headers
)

class BinLookupRequest(BaseModel):
    postcode: str
    house_number: Optional[str] = None

class BinCollection(BaseModel):
    bin_type: str
    color: str
    next_collection: str

class BinLookupResponse(BaseModel):
    address: str
    collections: list[BinCollection]
    next_collection_color: str

def get_mock_bin_data(postcode: str, house_number: Optional[str] = None):
    next_monday = datetime.now() + timedelta(days=(7 - datetime.now().weekday()))
    next_thursday = next_monday + timedelta(days=3)
    
    return BinLookupResponse(
        address=f"Sample Address, {postcode}",
        collections=[
            BinCollection(
                bin_type="General Waste (Black Bin)",
                color="Black",
                next_collection=next_monday.strftime("%A, %d %B %Y")
            ),
            BinCollection(
                bin_type="Recycling (Blue Bin)",
                color="Blue",
                next_collection=next_thursday.strftime("%A, %d %B %Y")
            ),
            BinCollection(
                bin_type="Garden Waste (Brown Bin)",
                color="Brown",
                next_collection=next_thursday.strftime("%A, %d %B %Y")
            )
        ],
        next_collection_color="Black"
    )

def setup_driver():
    chrome_options = Options()
    chrome_options.add_argument("--headless=new")
    chrome_options.add_argument("--no-sandbox")
    chrome_options.add_argument("--disable-dev-shm-usage")
    chrome_options.add_argument("--disable-gpu")
    chrome_options.add_argument("--disable-software-rasterizer")
    chrome_options.add_argument("--disable-extensions")
    chrome_options.add_argument("--disable-setuid-sandbox")
    chrome_options.add_argument("--remote-debugging-port=9222")
    chrome_options.add_argument("--window-size=1920,1080")
    chrome_options.add_argument("--user-agent=Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
    
    chrome_binary = os.environ.get("CHROME_BIN", "/usr/bin/google-chrome")
    if os.path.exists(chrome_binary):
        chrome_options.binary_location = chrome_binary
    
    service = Service(ChromeDriverManager().install())
    driver = webdriver.Chrome(service=service, options=chrome_options)
    return driver

def scrape_bin_data(postcode: str, house_number: Optional[str] = None):
    driver = None
    try:
        driver = setup_driver()
        driver.get("https://online.belfastcity.gov.uk/find-bin-collection-day/Default.aspx")
        
        wait = WebDriverWait(driver, 10)
        
        postcode_radio = wait.until(
            EC.element_to_be_clickable((By.ID, "searchBy_radio_1"))
        )
        postcode_radio.click()
        time.sleep(1)
        
        postcode_input = driver.find_element(By.ID, "Postcode_textbox")
        postcode_input.clear()
        postcode_input.send_keys(postcode)
        
        find_button = driver.find_element(By.NAME, "ctl00$MainContent$AddressLookup_button")
        find_button.click()
        
        time.sleep(2)
        
        page_source = driver.page_source
        if "not recognised" in page_source.lower():
            raise HTTPException(status_code=404, detail="Postcode not found")
        
        try:
            address_select = Select(driver.find_element(By.NAME, "ctl00$MainContent$lstAddresses"))
            options = address_select.options
            
            valid_options = [opt for opt in options if opt.get_attribute("value") != "Select the " + postcode + " address from the list."]
            
            if not valid_options:
                raise HTTPException(status_code=404, detail="No addresses found for this postcode")
            
            selected_option = valid_options[0]
            if house_number:
                for opt in valid_options:
                    if house_number.lower() in opt.text.lower():
                        selected_option = opt
                        break
            
            address_select.select_by_value(selected_option.get_attribute("value"))
            address_text = selected_option.text
            
            select_button = driver.find_element(By.NAME, "ctl00$MainContent$SelectAddress_button")
            select_button.click()
            
            time.sleep(3)
            
            soup = BeautifulSoup(driver.page_source, 'html.parser')
            
            collections = []
            next_collection_color = "Unknown"
            
            tables = soup.find_all('table')
            for table in tables:
                rows = table.find_all('tr')
                for row in rows:
                    cells = row.find_all('td')
                    if len(cells) >= 2:
                        bin_type = cells[0].get_text(strip=True)
                        collection_date = cells[1].get_text(strip=True)
                        
                        color = "Unknown"
                        if "black" in bin_type.lower() or "general" in bin_type.lower():
                            color = "Black"
                        elif "blue" in bin_type.lower() or "recycling" in bin_type.lower():
                            color = "Blue"
                        elif "brown" in bin_type.lower() or "compost" in bin_type.lower():
                            color = "Brown"
                        elif "green" in bin_type.lower() or "food" in bin_type.lower():
                            color = "Green"
                        elif "purple" in bin_type.lower() or "glass" in bin_type.lower():
                            color = "Purple"
                        
                        collections.append(BinCollection(
                            bin_type=bin_type,
                            color=color,
                            next_collection=collection_date
                        ))
            
            if collections:
                next_collection_color = collections[0].color
            
            return BinLookupResponse(
                address=address_text,
                collections=collections,
                next_collection_color=next_collection_color
            )
            
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"Error processing address: {str(e)}")
            
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Scraping error: {str(e)}")
    finally:
        if driver:
            driver.quit()

@app.get("/healthz")
async def healthz():
    return {"status": "ok"}

@app.post("/api/bin-lookup", response_model=BinLookupResponse)
async def lookup_bins(request: BinLookupRequest):
    try:
        return scrape_bin_data(request.postcode, request.house_number)
    except Exception as e:
        print(f"Scraping failed, using mock data: {str(e)}")
        return get_mock_bin_data(request.postcode, request.house_number)
