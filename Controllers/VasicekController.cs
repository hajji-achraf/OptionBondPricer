using App.Models;
using Microsoft.AspNetCore.Mvc;
using App.Models;

namespace VasicekBondApp.Controllers
{
    [Route("Obligation/[controller]")]
    public class VasicekController : Controller
    {
        public IActionResult Index()
        {
            return View(new VasicekModel());
        }

        [HttpPost]
        public IActionResult Calculate(VasicekModel model)
        {
            model.CalculateBondPrice();
            return View("Index", model);
        }
    }
}