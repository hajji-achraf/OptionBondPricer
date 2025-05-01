using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YahooFinanceApi;
using System.Linq;
using App.Models;
using Newtonsoft.Json;
using App.Models;

namespace App.Controllers
{
    [Route("Option/[controller]")]
    public class BlackScholesController : Controller
    {
        private const string YahooApiBaseUrl = "https://query1.finance.yahoo.com";
        private readonly ILogger<BlackScholesController> _logger;

        public BlackScholesController(ILogger<BlackScholesController> logger)
        {
            _logger = logger;
        }

        // Page principale
        [HttpGet]
        public IActionResult Index(string ticker = null, string optionSymbol = null)
        {
            var viewModel = new BlackScholesViewModel();
            return View(viewModel);
        }

        // Méthode pour rechercher des options basées sur un ticker
        [HttpPost("search")]
        public async Task<IActionResult> SearchOptions(string ticker)
        {
            if (string.IsNullOrEmpty(ticker))
            {
                return BadRequest("Le ticker est requis");
            }

            try
            {
                // Récupère les données du sous-jacent
                var security = await Yahoo.Symbols(ticker).Fields(
                    Field.Symbol,
                    Field.RegularMarketPrice,
                    Field.Currency).QueryAsync();

                var stockData = security[ticker];
                double currentPrice = stockData.RegularMarketPrice;
                string currency = stockData.Currency;

                // Récupération d'options
                var options = await GetOptionsForTicker(ticker, currentPrice);

                // Retourne les options au format JSON pour AJAX
                return Json(new
                {
                    success = true,
                    ticker = ticker,
                    spot = currentPrice,
                    currency = currency,
                    options = options
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recherche du ticker {Ticker}", ticker);
                return Json(new
                {
                    success = false,
                    error = $"Impossible de trouver des données pour le ticker {ticker}"
                });
            }
        }

        // Méthode pour valoriser une option spécifique
        [HttpPost("price")]
        public async Task<IActionResult> PriceOption([FromBody] BlackScholesViewModel model)
        {
            if (model == null)
            {
                return BadRequest("Le modèle est requis");
            }

            try
            {
                // Calcule les prix selon les différentes méthodes
                var prices = model.CalculateAllPrices();

                // Récupérer le prix du marché actuel si disponible
                try
                {
                    // Dans une application réelle, vous appelleriez ici l'API Yahoo Finance
                    // pour obtenir le prix actuel du marché
                    prices.MarketPrice = model.MarketPrice;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible de récupérer le prix de marché pour {Symbol}", model.OptionSymbol);
                }

                return Json(new
                {
                    success = true,
                    prices = prices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du pricing de l'option {Symbol}", model.OptionSymbol);
                return Json(new
                {
                    success = false,
                    error = "Impossible de calculer le prix de cette option: " + ex.Message
                });
            }
        }

        // Méthode pour obtenir les détails d'une option spécifique
        [HttpGet("details/{optionSymbol}")]
        public async Task<IActionResult> GetOptionDetails(string optionSymbol)
        {
            if (string.IsNullOrEmpty(optionSymbol))
            {
                return BadRequest("Le symbole de l'option est requis");
            }

            try
            {
                // Dans une implémentation réelle, vous appelleriez l'API Yahoo Finance ici
                // Pour l'exemple, extrayons les infos du symbole
                string ticker = optionSymbol.Substring(0, optionSymbol.Length > 4 ? 4 : optionSymbol.Length).TrimEnd('C', 'P');
                string optionType = optionSymbol.Contains('C') ? "Call" : "Put";

                // Extraction des informations (exemple simplifié)
                int dateStart = optionSymbol.IndexOf(optionType[0]) + 1;
                if (dateStart < 1 || dateStart + 6 > optionSymbol.Length)
                {
                    return BadRequest("Format du symbole d'option invalide");
                }

                string dateStr = optionSymbol.Substring(dateStart, 6);
                if (!int.TryParse(dateStr.Substring(0, 2), out int year) ||
                    !int.TryParse(dateStr.Substring(2, 2), out int month) ||
                    !int.TryParse(dateStr.Substring(4, 2), out int day))
                {
                    return BadRequest("Format de date invalide dans le symbole d'option");
                }

                DateTime expirationDate = new DateTime(2000 + year, month, day);

                // Calcul de la maturité en années
                double maturity = (expirationDate - DateTime.Now).TotalDays / 365.0;
                if (maturity <= 0)
                {
                    return BadRequest("Cette option est expirée");
                }

                // Extraction du strike
                int strikeStartIdx = dateStart + 6;
                if (strikeStartIdx >= optionSymbol.Length)
                {
                    return BadRequest("Format du symbole d'option invalide - prix d'exercice manquant");
                }

                if (!double.TryParse(optionSymbol.Substring(strikeStartIdx), out double strike))
                {
                    return BadRequest("Prix d'exercice invalide dans le symbole d'option");
                }

                // Récupération du prix actuel du sous-jacent
                var security = await Yahoo.Symbols(ticker).Fields(Field.RegularMarketPrice).QueryAsync();
                double spotPrice = security[ticker].RegularMarketPrice;

                var model = new BlackScholesViewModel
                {
                    Ticker = ticker,
                    OptionSymbol = optionSymbol,
                    OptionType = optionType,
                    Strike = strike,
                    Spot = spotPrice,
                    Maturity = maturity,
                    InterestRate = 2.5, // Taux d'intérêt par défaut (%)
                    Volatility = 25.0,  // Volatilité implicite par défaut (%)
                    Simulations = 10000 // Nombre de simulations par défaut
                };

                // Récupérer les données historiques pour estimer la volatilité
                try
                {
                    var history = await Yahoo.GetHistoricalAsync(ticker, DateTime.Now.AddMonths(-3), DateTime.Now, Period.Daily);

                    if (history.Count > 20)
                    {
                        var returns = new List<double>();

                        for (int i = 1; i < history.Count; i++)
                        {
                            returns.Add(Math.Log((double)history[i].Close / (double)history[i - 1].Close));
                        }

                        double stdDev = Math.Sqrt(returns.Select(r => r * r).Sum() / returns.Count);
                        model.Volatility = stdDev * Math.Sqrt(252) * 100; // Annualiser et convertir en pourcentage
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible d'estimer la volatilité historique pour {Ticker}", ticker);
                }

                // Simulation d'un prix de marché
                try
                {
                    model.MarketPrice = model.PriceByAnalytical() * (0.95 + 0.1 * new Random().NextDouble());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible de simuler un prix de marché pour {Symbol}", optionSymbol);
                    model.MarketPrice = 0;
                }

                return Json(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des détails de l'option {Symbol}", optionSymbol);
                return Json(new
                {
                    success = false,
                    error = $"Erreur lors de la récupération des détails: {ex.Message}"
                });
            }
        }

        // Méthode pour obtenir une liste d'options pour un ticker donné
        private async Task<List<OptionSummary>> GetOptionsForTicker(string ticker, double currentPrice)
        {
            var options = new List<OptionSummary>();

            try
            {
                // Dans une implémentation réelle, vous devriez appeler l'API Yahoo Finance Options Chain
                // Pour l'exemple, nous générons des options fictives
                DateTime now = DateTime.Now;

                // Ajouter des expirations standard : 1 mois, 3 mois, 6 mois
                DateTime[] expirations = new[]
                {
                    now.AddMonths(1),
                    now.AddMonths(3),
                    now.AddMonths(6)
                };

                // Générer différents strikes autour du prix actuel
                for (int expIdx = 0; expIdx < expirations.Length; expIdx++)
                {
                    DateTime exp = expirations[expIdx];
                    double maturity = (exp - now).TotalDays / 365.0;

                    for (int i = -3; i <= 3; i++)
                    {
                        // Strike price = 5% d'écart entre les différents strikes
                        double strike = Math.Round(currentPrice * (1 + 0.05 * i), 2);

                        // Option call
                        options.Add(new OptionSummary
                        {
                            OptionSymbol = $"{ticker}C{exp:yyMMdd}{strike:F0}",
                            Strike = strike,
                            Maturity = maturity,
                            OptionType = "Call",
                            ExpirationDate = exp
                        });

                        // Option put correspondante
                        options.Add(new OptionSummary
                        {
                            OptionSymbol = $"{ticker}P{exp:yyMMdd}{strike:F0}",
                            Strike = strike,
                            Maturity = maturity,
                            OptionType = "Put",
                            ExpirationDate = exp
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération des options pour {Ticker}", ticker);
            }

            return options;
        }
    }
}