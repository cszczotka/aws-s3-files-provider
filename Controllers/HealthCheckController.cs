using System;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;


namespace EJ2FileManagerServices.Controllers
{
    [Route("api/[controller]")]
    public class HealthCheckController : Controller
    {
        [HttpGet]
        public string Status()
        {
            var localDate = DateTime.Now;
            var culture = new CultureInfo("en-US");
            return $"System Up -> {localDate.ToString(culture)}";
        }
    }
}
