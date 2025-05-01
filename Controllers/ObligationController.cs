using Microsoft.AspNetCore.Mvc;

namespace App.Controllers
{
    public class ObligationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
