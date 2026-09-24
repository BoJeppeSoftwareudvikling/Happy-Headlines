using Microsoft.AspNetCore.Mvc;
using NewsletterService.Services;

namespace NewsletterService.Controllers;

[ApiController]
[Route("[controller]")]
public class NewsletterController(NewsletterFeed feed) : ControllerBase
{
    [HttpGet("latest")]
    public IActionResult GetLatest() => Ok(feed.GetLatest());

    [HttpGet("instance")]
    public IActionResult Instance() => Ok(new { Hostname = Environment.MachineName });
}
