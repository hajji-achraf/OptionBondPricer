using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YahooFinanceApi;
using System.Linq;
using App.Models;
using Newtonsoft.Json;

namespace App.Controllers
{
    [Route("Option/[controller]")]
    public class HestonController : Controller
    {
        private const string YahooApiBaseUrl = "https://query1.finance.yahoo.com";
        private readonly ILogger<HestonController> _logger;

        public HestonController(ILogger<HestonController> logger)
        {
            _logger = logger;
        }

        // Page principale
        [HttpGet]
        public IActionResult Index(string ticker = null, string optionSymbol = null)
        {
            var viewModel = new HestonViewModel();
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
        public async Task<IActionResult> PriceOption([FromBody] HestonViewModel model)
        {
            if (model == null)
            {
                return BadRequest("Le modèle est requis");
            }

            try
            {
                // Calcule les prix selon les différentes méthodes
                var prices = model.CalculateAllPrices();

                // Calcule la surface de volatilité
                prices.VolatilitySurface = CalculateVolatilitySurface(model);

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
                _logger.LogError(ex, "Erreur lors du pricing de l'option {Symbol} avec Heston: {Message}",
                    model.OptionSymbol, ex.Message);
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

                // Créer le modèle Heston avec paramètres par défaut
                var model = new HestonViewModel
                {
                    Ticker = ticker,
                    OptionSymbol = optionSymbol,
                    OptionType = optionType,
                    Strike = strike,
                    Spot = spotPrice,
                    Maturity = maturity,
                    InterestRate = 2.5,   // Taux d'intérêt par défaut (%)
                    V0 = 25.0,            // Volatilité initiale par défaut (%)
                    Kappa = 1.5,          // Taux de retour à la moyenne par défaut
                    Theta = 0.04,         // Niveau moyen de volatilité par défaut (en variance)
                    Sigma = 0.3,          // Volatilité de la volatilité par défaut
                    Rho = -0.7,           // Corrélation par défaut
                    Simulations = 10000   // Nombre de simulations par défaut
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
                        model.V0 = stdDev * Math.Sqrt(252) * 100; // Annualiser et convertir en pourcentage
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible d'estimer la volatilité historique pour {Ticker}", ticker);
                }

                // Simulation d'un prix de marché
                try
                {
                    // Utiliser modèle.PriceBySemiAnalytical() mais ce n'est pas accessible
                    // Donc on utilise une approximation
                    model.MarketPrice = model.CalculateAllPrices().SemiAnalyticalPrice * (0.95 + 0.1 * new Random().NextDouble());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible de simuler un prix de marché pour {Symbol}", optionSymbol);
                    model.MarketPrice = 0;
                }

                return Json(new
                {
                    success = true,
                    optionSymbol = model.OptionSymbol,
                    ticker = model.Ticker,
                    optionType = model.OptionType,
                    strike = model.Strike,
                    spot = model.Spot,
                    maturity = model.Maturity,
                    interestRate = model.InterestRate,
                    v0 = model.V0,
                    kappa = model.Kappa,
                    theta = model.Theta,
                    sigma = model.Sigma,
                    rho = model.Rho,
                    simulations = model.Simulations,
                    marketPrice = model.MarketPrice
                });
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

        // Méthode pour calculer la surface de volatilité
        private VolatilitySurfaceData CalculateVolatilitySurface(HestonViewModel model)
        {
            var result = new VolatilitySurfaceData
            {
                Strikes = new List<double>(),
                Maturities = new List<double>(),
                ImpliedVolatilities = new List<double>()
            };

            // Base strike et maturity
            double baseStrike = model.Strike;
            double baseMaturity = model.Maturity;

            // Plages de strikes et maturités
            for (int i = -7; i <= 7; i++)
            {
                double strikeModifier = 1 + (i * 0.05); // -35% à +35% du strike
                double strike = baseStrike * strikeModifier;

                for (int j = 1; j <= 12; j++)
                {
                    double maturity = j * 0.1; // 0.1 à 1.2 ans

                    // Ajouter les valeurs des axes
                    result.Strikes.Add(strike);
                    result.Maturities.Add(maturity);

                    // Calculer la volatilité implicite
                    double impliedVol = CalculateHestonImpliedVol(model, strike, maturity);

                    result.ImpliedVolatilities.Add(impliedVol);
                }
            }

            return result;
        }

        // Calculer la volatilité implicite du modèle de Heston
        private double CalculateHestonImpliedVol(HestonViewModel model, double strike, double maturity)
        {
            bool isCall = model.OptionType == "Call";
            double r = model.InterestRate / 100.0;
            double spot = model.Spot;

            // Moneyness (ratio entre le prix d'exercice et le sous-jacent)
            double moneyness = strike / spot;

            // Effet de smile de volatilité (forme de U)
            double smileEffect = 0.03 * Math.Pow(Math.Log(moneyness), 2);

            // Effet de Skew (asymétrie tipique - volatilité plus élévée pour les strikes bas)
            double skewEffect = -0.05 * Math.Log(moneyness);

            // Effet de term structure (la volatilité tends vers theta à long terme)
            double termEffect = (Math.Sqrt(model.V0 / 100.0) - Math.Sqrt(model.Theta)) * Math.Exp(-model.Kappa * maturity);

            // Volatilité implicite (volatilité à long terme + effets)
            double baseVol = model.V0 / 100.0; // Volatilité initiale
            double impliedVol = baseVol + smileEffect + skewEffect * model.Rho + termEffect;

            // Limiter la volatilité à des valeurs raisonnables
            return Math.Max(0.05, Math.Min(0.8, impliedVol)) * 100; // Retourner en pourcentage
        }
    }
}