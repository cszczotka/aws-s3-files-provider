using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace EJ2FileManagerServices.Controllers
{
    [Route("api/[controller]")]
    public class HealthCheckController : Controller
    {
        // GET api/values
        [HttpGet]
        public string Status()
        {
            return "OK";
        }
    }
    
}
