using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

namespace App.Models
{
    public class BlackScholesViewModel
    {
        // Propriétés de base
        public string Ticker { get; set; }
        public string OptionSymbol { get; set; }

        [Display(Name = "Type d'option")]
        public string OptionType { get; set; } = "Call"; // Call ou Put

        [Display(Name = "Prix d'exercice")]
        public double Strike { get; set; }

        [Display(Name = "Prix du sous-jacent")]
        public double Spot { get; set; }

        [Display(Name = "Maturité (années)")]
        public double Maturity { get; set; }

        [Display(Name = "Taux sans risque (%)")]
        public double InterestRate { get; set; } = 2.5;

        [Display(Name = "Volatilité (%)")]
        public double Volatility { get; set; } = 25;

        [Display(Name = "Nombre de simulations")]
        public int Simulations { get; set; } = 10000;

        [Display(Name = "Prix du marché")]
        public double MarketPrice { get; set; }

        // Résultats des calculs
        public double AnalyticalPrice { get; set; }
        public double MonteCarloPrice { get; set; }
        public double EulerPrice { get; set; }

        // Greeks
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
        public double Rho { get; set; }

        // Liste des options disponibles pour un ticker
        public List<OptionSummary> AvailableOptions { get; set; } = new List<OptionSummary>();

        // Données pour les graphiques
        public List<ChartDataPoint> PriceBySpot { get; set; } = new List<ChartDataPoint>();
        public List<ChartDataPoint> PriceByVolatility { get; set; } = new List<ChartDataPoint>();
        public List<ChartDataPoint> PriceByTime { get; set; } = new List<ChartDataPoint>();
        public List<SurfaceDataPoint> VolatilitySurface { get; set; } = new List<SurfaceDataPoint>();

        // Méthode de calcul analytique (solution fermée de Black-Scholes)
        public double PriceByAnalytical()
        {
            // Conversion des pourcentages en décimales
            double sigma = Volatility / 100;
            double r = InterestRate / 100;

            // Gestion des cas limites
            if (Maturity <= 0 || Strike <= 0 || Spot <= 0 || sigma <= 0)
            {
                return 0;
            }

            double d1 = (Math.Log(Spot / Strike) + (r + sigma * sigma / 2) * Maturity) / (sigma * Math.Sqrt(Maturity));
            double d2 = d1 - sigma * Math.Sqrt(Maturity);

            double price = 0;
            if (OptionType == "Call")
            {
                price = Spot * Normal.CDF(0, 1, d1) - Strike * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, d2);

                // Calcul des Greeks
                Delta = Normal.CDF(0, 1, d1);
                Gamma = Normal.PDF(0, 1, d1) / (Spot * sigma * Math.Sqrt(Maturity));
                Theta = -(Spot * Normal.PDF(0, 1, d1) * sigma) / (2 * Math.Sqrt(Maturity)) -
                        r * Strike * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, d2);
                Vega = Spot * Math.Sqrt(Maturity) * Normal.PDF(0, 1, d1) / 100; // en % pour correspondre à la volatilité
                Rho = Strike * Maturity * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, d2) / 100; // en %
            }
            else // Put
            {
                price = Strike * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, -d2) - Spot * Normal.CDF(0, 1, -d1);

                // Calcul des Greeks
                Delta = Normal.CDF(0, 1, d1) - 1;
                Gamma = Normal.PDF(0, 1, d1) / (Spot * sigma * Math.Sqrt(Maturity));
                Theta = -(Spot * Normal.PDF(0, 1, d1) * sigma) / (2 * Math.Sqrt(Maturity)) +
                        r * Strike * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, -d2);
                Vega = Spot * Math.Sqrt(Maturity) * Normal.PDF(0, 1, d1) / 100; // en % pour correspondre à la volatilité
                Rho = -Strike * Maturity * Math.Exp(-r * Maturity) * Normal.CDF(0, 1, -d2) / 100; // en %
            }

            return Math.Round(price, 2);
        }

        // Méthode de calcul par simulation Monte Carlo
        public double PriceByMonteCarlo()
        {
            // Conversion des pourcentages en décimales
            double sigma = Volatility / 100;
            double r = InterestRate / 100;

            // Gestion des cas limites
            if (Maturity <= 0 || Strike <= 0 || Spot <= 0 || sigma <= 0)
            {
                return 0;
            }

            double dt = Maturity;
            double sqrtDt = Math.Sqrt(dt);

            var randomSource = new MersenneTwister();
            var normal = new Normal(0, 1, randomSource);

            double sum = 0;
            double sumSquared = 0; // Pour calculer la variance

            for (int i = 0; i < Simulations; i++)
            {
                double z = normal.Sample();
                double stockPrice = Spot * Math.Exp((r - 0.5 * sigma * sigma) * dt + sigma * sqrtDt * z);

                double payoff = 0;
                if (OptionType == "Call")
                {
                    payoff = Math.Max(stockPrice - Strike, 0);
                }
                else // Put
                {
                    payoff = Math.Max(Strike - stockPrice, 0);
                }

                sum += payoff;
                sumSquared += payoff * payoff;
            }

            double price = Math.Exp(-r * Maturity) * (sum / Simulations);
            // Calcul de l'écart type pour l'intervalle de confiance
            double variance = (sumSquared / Simulations) - Math.Pow(sum / Simulations, 2);
            double stdError = Math.Sqrt(variance / Simulations);

            return Math.Round(price, 2);
        }

        // Méthode de calcul par schéma d'Euler
        public double PriceByEuler()
        {
            // Conversion des pourcentages en décimales
            double sigma = Volatility / 100;
            double r = InterestRate / 100;

            // Gestion des cas limites
            if (Maturity <= 0 || Strike <= 0 || Spot <= 0 || sigma <= 0)
            {
                return 0;
            }

            int steps = 50; // Nombre d'étapes pour la discrétisation
            double dt = Maturity / steps;
            double sqrtDt = Math.Sqrt(dt);

            var randomSource = new MersenneTwister();
            var normal = new Normal(0, 1, randomSource);

            double sum = 0;

            for (int i = 0; i < Simulations; i++)
            {
                double stockPrice = Spot;

                for (int j = 0; j < steps; j++)
                {
                    double z = normal.Sample();
                    stockPrice += r * stockPrice * dt + sigma * stockPrice * sqrtDt * z;
                }

                double payoff = 0;
                if (OptionType == "Call")
                {
                    payoff = Math.Max(stockPrice - Strike, 0);
                }
                else // Put
                {
                    payoff = Math.Max(Strike - stockPrice, 0);
                }

                sum += payoff;
            }

            double price = Math.Exp(-r * Maturity) * (sum / Simulations);
            return Math.Round(price, 2);
        }

        // Méthode pour calculer les trois prix et mettre à jour les données pour les graphiques
        public BlackScholesPrices CalculateAllPrices()
        {
            AnalyticalPrice = PriceByAnalytical();
            MonteCarloPrice = PriceByMonteCarlo();
            EulerPrice = PriceByEuler();

            // Génération des données pour les graphiques
            GenerateChartData();

            return new BlackScholesPrices
            {
                AnalyticalPrice = AnalyticalPrice,
                MonteCarloPrice = MonteCarloPrice,
                EulerPrice = EulerPrice,
                MarketPrice = MarketPrice,
                Delta = Delta,
                Gamma = Gamma,
                Theta = Theta,
                Vega = Vega,
                Rho = Rho,
                PriceBySpot = PriceBySpot,
                PriceByVolatility = PriceByVolatility,
                PriceByTime = PriceByTime,
                VolatilitySurface = VolatilitySurface
            };
        }

        // Génération des données pour les graphiques
        private void GenerateChartData()
        {
            // 1. Prix en fonction du prix du sous-jacent
            PriceBySpot = new List<ChartDataPoint>();
            double minSpot = Spot * 0.7;
            double maxSpot = Spot * 1.3;
            double stepSpot = (maxSpot - minSpot) / 20;

            for (double s = minSpot; s <= maxSpot; s += stepSpot)
            {
                double originalSpot = Spot;
                Spot = s;
                double price = PriceByAnalytical();
                Spot = originalSpot; // Restaurer la valeur originale

                PriceBySpot.Add(new ChartDataPoint { X = s, Y = price });
            }

            // 2. Prix en fonction de la volatilité
            PriceByVolatility = new List<ChartDataPoint>();
            double minVol = Math.Max(5, Volatility * 0.5);
            double maxVol = Volatility * 1.5;
            double stepVol = (maxVol - minVol) / 20;

            for (double v = minVol; v <= maxVol; v += stepVol)
            {
                double originalVol = Volatility;
                Volatility = v;
                double price = PriceByAnalytical();
                Volatility = originalVol; // Restaurer la valeur originale

                PriceByVolatility.Add(new ChartDataPoint { X = v, Y = price });
            }

            // 3. Prix en fonction du temps jusqu'à expiration (constant strike)
            PriceByTime = new List<ChartDataPoint>();
            double minTime = Math.Max(0.01, Maturity * 0.1);
            double maxTime = Maturity * 1.2;
            double stepTime = (maxTime - minTime) / 20;

            for (double t = minTime; t <= maxTime; t += stepTime)
            {
                double originalMaturity = Maturity;
                Maturity = t;
                double price = PriceByAnalytical();
                Maturity = originalMaturity; // Restaurer la valeur originale

                PriceByTime.Add(new ChartDataPoint { X = t, Y = price });
            }

            // 4. Surface de volatilité implicite (3D)
            VolatilitySurface = new List<SurfaceDataPoint>();
            double[] strikes = new double[10];
            double[] maturities = new double[10];

            for (int i = 0; i < 10; i++)
            {
                strikes[i] = Strike * (0.8 + 0.04 * i);
                maturities[i] = Maturity * (0.5 + 0.1 * i);
            }

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    // Simulons une surface de volatilité réaliste (smile)
                    double vol = Volatility * (1 + 0.1 * Math.Pow((strikes[j] / Spot - 1), 2) - 0.05 * maturities[i]);
                    vol = Math.Max(10, Math.Min(50, vol)); // Limiter entre 10% et 50%

                    VolatilitySurface.Add(new SurfaceDataPoint
                    {
                        X = strikes[j],
                        Y = maturities[i],
                        Z = vol
                    });
                }
            }
        }
    }

    public class BlackScholesPrices
    {
        public double AnalyticalPrice { get; set; }
        public double MonteCarloPrice { get; set; }
        public double EulerPrice { get; set; }
        public double MarketPrice { get; set; }

        // Greeks
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double Vega { get; set; }
        public double Rho { get; set; }

        // Données pour les graphiques
        public List<ChartDataPoint> PriceBySpot { get; set; }
        public List<ChartDataPoint> PriceByVolatility { get; set; }
        public List<ChartDataPoint> PriceByTime { get; set; }
        public List<SurfaceDataPoint> VolatilitySurface { get; set; }
    }

    // Classe pour représenter un résumé d'option pour l'affichage en liste
    public class OptionSummary
    {
        public string OptionSymbol { get; set; }
        public double Strike { get; set; }
        public string OptionType { get; set; }
        public double Maturity { get; set; }
        public DateTime ExpirationDate { get; set; }

        // Formatage pour l'affichage
        public string DisplayName => $"{OptionType} {Strike} ({ExpirationDate.ToString("dd/MM/yyyy")})";
    }

    // Classes pour les données de graphiques
    public class ChartDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class SurfaceDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}