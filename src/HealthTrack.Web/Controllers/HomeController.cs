using Microsoft.AspNetCore.Mvc;
namespace HealthTrack.Web.Controllers;
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();
}
