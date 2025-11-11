using Microsoft.AspNetCore.Mvc;
using BelfastBinsApi.Models;
using BelfastBinsApi.Services;

namespace BelfastBinsApi.Controllers;

[ApiController]
[Route("api")]
public class BinLookupController : ControllerBase
{
    private readonly BinScraperService _scraperService;
    private readonly ILogger<BinLookupController> _logger;

    public BinLookupController(BinScraperService scraperService, ILogger<BinLookupController> logger)
    {
        _scraperService = scraperService;
        _logger = logger;
    }

    [HttpGet("healthz")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "ok" });
    }

    [HttpPost("bin-lookup")]
    public async Task<ActionResult<BinLookupResponse>> LookupBins([FromBody] BinLookupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Postcode))
            {
                return BadRequest(new { detail = "Postcode is required" });
            }

            _logger.LogInformation($"Bin lookup request: {request.Postcode} {request.HouseNumber}");

            var dbResult = await _scraperService.LookupFromDatabase(request.Postcode, request.HouseNumber);
            if (dbResult != null)
            {
                _logger.LogInformation("Found in database, returning result");
                return Ok(dbResult);
            }

            _logger.LogInformation("Not found in database, falling back to web scraping");

            try
            {
                var result = await _scraperService.ScrapeBinData(request.Postcode, request.HouseNumber);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scraping failed, using mock data");
                var mockResult = _scraperService.GetMockBinData(request.Postcode, request.HouseNumber);
                return Ok(mockResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bin lookup request");
            return StatusCode(500, new { detail = $"Error: {ex.Message}" });
        }
    }
}
