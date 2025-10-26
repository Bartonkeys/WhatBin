using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using HtmlAgilityPack;
using BelfastBinsApi.Models;

namespace BelfastBinsApi.Services;

public class BinScraperService
{
    private readonly ILogger<BinScraperService> _logger;

    public BinScraperService(ILogger<BinScraperService> logger)
    {
        _logger = logger;
    }

    public BinLookupResponse GetMockBinData(string postcode, string? houseNumber = null)
    {
        var nextMonday = DateTime.Now.AddDays((7 - (int)DateTime.Now.DayOfWeek) % 7);
        if (nextMonday.Date == DateTime.Now.Date)
            nextMonday = nextMonday.AddDays(7);
        
        var nextThursday = nextMonday.AddDays(3);

        return new BinLookupResponse
        {
            Address = $"Sample Address, {postcode}",
            Collections = new List<BinCollection>
            {
                new BinCollection
                {
                    BinType = "General Waste (Black Bin)",
                    Color = "Black",
                    NextCollection = nextMonday.ToString("dddd, dd MMMM yyyy")
                },
                new BinCollection
                {
                    BinType = "Recycling (Blue Bin)",
                    Color = "Blue",
                    NextCollection = nextThursday.ToString("dddd, dd MMMM yyyy")
                },
                new BinCollection
                {
                    BinType = "Garden Waste (Brown Bin)",
                    Color = "Brown",
                    NextCollection = nextThursday.ToString("dddd, dd MMMM yyyy")
                }
            },
            NextCollectionColor = "Black"
        };
    }

    private IWebDriver SetupDriver()
    {
        var chromeOptions = new ChromeOptions();
        chromeOptions.AddArgument("--headless=new");
        chromeOptions.AddArgument("--no-sandbox");
        chromeOptions.AddArgument("--disable-dev-shm-usage");
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--disable-software-rasterizer");
        chromeOptions.AddArgument("--disable-extensions");
        chromeOptions.AddArgument("--disable-setuid-sandbox");
        chromeOptions.AddArgument("--remote-debugging-port=9222");
        chromeOptions.AddArgument("--window-size=1920,1080");
        chromeOptions.AddArgument("--user-agent=Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        var chromeBinary = Environment.GetEnvironmentVariable("CHROME_BIN") ?? "/usr/bin/google-chrome";
        if (File.Exists(chromeBinary))
        {
            chromeOptions.BinaryLocation = chromeBinary;
        }

        var driver = new ChromeDriver(chromeOptions);
        return driver;
    }

    public async Task<BinLookupResponse> ScrapeBinData(string postcode, string? houseNumber = null)
    {
        IWebDriver? driver = null;
        try
        {
            driver = SetupDriver();
            driver.Navigate().GoToUrl("https://online.belfastcity.gov.uk/find-bin-collection-day/Default.aspx");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var js = (IJavaScriptExecutor)driver;

            wait.Until(d => js.ExecuteScript("return document.readyState")?.ToString() == "complete");

            try
            {
                var cookieBtn = driver.FindElements(By.CssSelector("button[aria-label='Accept all cookies'], .cookie-accept, #onetrust-accept-btn-handler")).FirstOrDefault();
                if (cookieBtn != null && cookieBtn.Displayed)
                {
                    cookieBtn.Click();
                    await Task.Delay(500);
                }
            }
            catch
            {
            }

            var postcodeRadioLabel = wait.Until(d => d.FindElement(By.CssSelector("label[for='searchBy_radio_1']")));
            js.ExecuteScript("arguments[0].scrollIntoView({block:'center', inline:'center'});", postcodeRadioLabel);

            try
            {
                postcodeRadioLabel.Click();
            }
            catch (ElementClickInterceptedException)
            {
                js.ExecuteScript("arguments[0].click();", postcodeRadioLabel);
            }

            var postcodeInput = wait.Until(d =>
            {
                var el = d.FindElement(By.Id("Postcode_textbox"));
                return (el.Displayed && el.Enabled) ? el : null;
            });

            postcodeInput.Clear();
            postcodeInput.SendKeys(postcode);

            var findButton = driver.FindElement(By.Name("ctl00$MainContent$AddressLookup_button"));
            findButton.Click();

            wait.Until(d =>
            {
                var pageSource = d.PageSource;
                return pageSource.ToLower().Contains("not recognised") || 
                       d.FindElements(By.Name("ctl00$MainContent$lstAddresses")).Any();
            });

            var pageSource = driver.PageSource;
            if (pageSource.ToLower().Contains("not recognised"))
            {
                throw new Exception("Postcode not found");
            }

            try
            {
                var addressSelect = new SelectElement(wait.Until(d => d.FindElement(By.Name("ctl00$MainContent$lstAddresses"))));
                var options = addressSelect.Options;

                var validOptions = options.Where(opt => 
                {
                    var optText = opt.Text ?? "";
                    return !optText.ToLower().Contains("select the");
                }).ToList();

                _logger.LogInformation($"Found {validOptions.Count} valid address options");
                foreach (var opt in validOptions.Take(3))
                {
                    _logger.LogInformation($"Option: {opt.Text} (value: {opt.GetAttribute("value")})");
                }

                if (!validOptions.Any())
                {
                    throw new Exception("No addresses found for this postcode");
                }

                IWebElement selectedOption = validOptions[0];
                if (!string.IsNullOrEmpty(houseNumber))
                {
                    foreach (var opt in validOptions)
                    {
                        if (opt.Text.ToLower().Contains(houseNumber.ToLower()))
                        {
                            selectedOption = opt;
                            break;
                        }
                    }
                }

                var addressText = selectedOption.Text;
                _logger.LogInformation($"Selecting address: {addressText}");
                
                addressSelect.SelectByText(addressText);
                
                var verifySelection = addressSelect.SelectedOption.Text;
                _logger.LogInformation($"Verified selection: {verifySelection}");

                var selectButton = driver.FindElement(By.Name("ctl00$MainContent$SelectAddress_button"));
                js.ExecuteScript("arguments[0].scrollIntoView({block:'center', inline:'center'});", selectButton);
                
                try
                {
                    selectButton.Click();
                }
                catch (ElementClickInterceptedException)
                {
                    _logger.LogInformation("Normal click intercepted, using JS click");
                    js.ExecuteScript("arguments[0].click();", selectButton);
                }

                wait.Until(d => 
                {
                    var resultsRows = d.FindElements(By.CssSelector("#ItemsGrid tr"));
                    var errorMsg = d.FindElements(By.Id("lblMessage")).FirstOrDefault();
                    return resultsRows.Count > 1 || (errorMsg != null && errorMsg.Displayed);
                });

                var errorElement = driver.FindElements(By.Id("lblMessage")).FirstOrDefault();
                if (errorElement != null && errorElement.Displayed && !string.IsNullOrWhiteSpace(errorElement.Text))
                {
                    _logger.LogWarning($"Error message displayed: {errorElement.Text}");
                    throw new Exception($"Bin collection data not available: {errorElement.Text}");
                }

                var collections = new List<BinCollection>();
                var nextCollectionColor = "Unknown";

                _logger.LogInformation("Parsing bin collection data using Selenium");
                
                var resultsTable = driver.FindElements(By.CssSelector("#ItemsGrid tr"));
                _logger.LogInformation($"Found {resultsTable.Count} rows in ItemsGrid");
                
                for (int i = 1; i < resultsTable.Count; i++)
                {
                    var row = resultsTable[i];
                    var cells = row.FindElements(By.TagName("td"));
                    
                    if (cells.Count >= 2)
                    {
                        var binType = cells[0].Text.Trim();
                        var collectionDate = cells[1].Text.Trim();

                        _logger.LogInformation($"Found bin: {binType} - {collectionDate}");

                        var color = "Unknown";
                        var binTypeLower = binType.ToLower();
                        if (binTypeLower.Contains("black") || binTypeLower.Contains("general"))
                            color = "Black";
                        else if (binTypeLower.Contains("blue") || binTypeLower.Contains("recycling"))
                            color = "Blue";
                        else if (binTypeLower.Contains("brown") || binTypeLower.Contains("compost"))
                            color = "Brown";
                        else if (binTypeLower.Contains("green") || binTypeLower.Contains("food"))
                            color = "Green";
                        else if (binTypeLower.Contains("purple") || binTypeLower.Contains("glass"))
                            color = "Purple";

                        collections.Add(new BinCollection
                        {
                            BinType = binType,
                            Color = color,
                            NextCollection = collectionDate
                        });
                    }
                }

                if (collections.Any())
                {
                    nextCollectionColor = collections[0].Color;
                }

                return new BinLookupResponse
                {
                    Address = addressText,
                    Collections = collections,
                    NextCollectionColor = nextCollectionColor
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing address");
                throw new Exception($"Error processing address: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scraping error");
            throw new Exception($"Scraping error: {ex.Message}");
        }
        finally
        {
            driver?.Quit();
        }
    }
}
