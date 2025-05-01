using Microsoft.AspNetCore.Mvc;
using App.Models;

namespace App.Controllers
{
    [Route("Obligation/[controller]")]
    public class HullWhiteController : Controller
    {
        public IActionResult Index()
        {
            return View(new HullWhiteModel());
        }

        [HttpPost]
        public IActionResult Calculate(HullWhiteModel model)
        {
            model.CalculateBondPrice();
            return View("Index", model);
        }
    }
}