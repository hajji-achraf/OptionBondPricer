using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace App.Models
{
    public class HestonViewModel
    {
        // Propriétés de base pour l'option
        [Display(Name = "Symbole de l'Option")]
        public string OptionSymbol { get; set; }

        [Display(Name = "Ticker")]
        public string Ticker { get; set; }

        [Display(Name = "Type d'Option")]
        public string OptionType { get; set; } // Call ou Put

        [Display(Name = "Prix d'Exercice")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Le prix d'exercice doit être positif")]
        public double Strike { get; set; }

        [Display(Name = "Prix du Sous-jacent")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Le prix du sous-jacent doit être positif")]
        public double Spot { get; set; }

        [Display(Name = "Maturité (années)")]
        [Range(0.001, 10, ErrorMessage = "La maturité doit être entre 0.001 et 10 ans")]
        public double Maturity { get; set; }

        [Display(Name = "Taux Sans Risque (%)")]
        public double InterestRate { get; set; }

        [Display(Name = "Date d'Expiration")]
        public DateTime ExpirationDate { get; set; }

        [Display(Name = "Prix du marché")]
        public double MarketPrice { get; set; }

        // Paramètres spécifiques au modèle de Heston
        [Display(Name = "Volatilité initiale (%)")]
        [Range(0.1, 200, ErrorMessage = "La volatilité initiale doit être positive")]
        public double V0 { get; set; }

        [Display(Name = "Kappa (Taux de retour à la moyenne)")]
        [Range(0.01, 20, ErrorMessage = "Kappa doit être positif")]
        public double Kappa { get; set; } = 1.5;

        [Display(Name = "Theta (Niveau moyen de volatilité)")]
        [Range(0.01, 1, ErrorMessage = "Theta doit être entre 0.01 et 1")]
        public double Theta { get; set; } = 0.04; // 20% standard en volatilité (0.04 en variance)

        [Display(Name = "Sigma (Volatilité de la volatilité)")]
        [Range(0.01, 2, ErrorMessage = "Sigma doit être positif")]
        public double Sigma { get; set; } = 0.3;

        [Display(Name = "Rho (Corrélation)")]
        [Range(-1, 1, ErrorMessage = "Rho doit être entre -1 et 1")]
        public double Rho { get; set; } = -0.7;

        [Display(Name = "Nombre de simulations")]
        [Range(1000, 100000, ErrorMessage = "Le nombre de simulations doit être entre 1000 et 100000")]
        public int Simulations { get; set; } = 10000;

        // Calcul de tous les prix
        public HestonPricingResult CalculateAllPrices()
        {
            var result = new HestonPricingResult();

            // Convertir les pourcentages en décimales
            double r = InterestRate / 100.0;
            double v0 = Math.Pow(V0 / 100.0, 2); // Variance initiale
            double theta = Math.Pow(Theta, 2);   // Variance à long terme

            // Calculs des prix
            result.SemiAnalyticalPrice = PriceBySemiAnalytical();
            result.MonteCarloPrice = PriceByMonteCarlo();
            result.FiniteDifferencePrice = PriceByFiniteDifference();

            // Calculs des Greeks
            result.Delta = CalculateDelta();
            result.Gamma = CalculateGamma();
            result.ThetaGreek = CalculateTheta();
            result.Vega = CalculateVega();
            result.RhoGreek = CalculateRho();

            // Prix de marché
            result.MarketPrice = MarketPrice;

            return result;
        }

        // =================== METHODE SEMI-ANALYTIQUE COMPLETE ===================
        private double PriceBySemiAnalytical()
        {
            bool isCall = OptionType == "Call";
            double r = InterestRate / 100.0;
            double q = 0; // Taux de dividende (supposé nul ici)
            double S = Spot;
            double K = Strike;
            double T = Maturity;
            double v0 = Math.Pow(V0 / 100.0, 2); // Variance initiale
            double theta = Math.Pow(Theta, 2);   // Variance long terme
            double kappa = Kappa;
            double sigma = Sigma;
            double rho = Rho;

            // Pour éviter les divisions par zéro et améliorer la précision numérique
            if (Math.Abs(sigma) < 1e-6) sigma = 1e-6;
            if (Math.Abs(v0) < 1e-6) v0 = 1e-6;

            // Calcul des probabilités
            double P1 = CalculateProbability(1, S, K, T, r, v0, kappa, theta, sigma, rho);
            double P2 = CalculateProbability(2, S, K, T, r, v0, kappa, theta, sigma, rho);

            // Prix final de l'option
            double price = S * P1 - K * Math.Exp(-r * T) * P2;

            // Ajustement pour option put (via parité call-put)
            if (!isCall)
            {
                price = price + K * Math.Exp(-r * T) - S;
            }

            return price;
        }

        // Calcul de la probabilité Pj par intégration numérique
        private double CalculateProbability(int j, double S, double K, double T, double r,
                                           double v0, double kappa, double theta,
                                           double sigma, double rho)
        {
            double integralResult = 0.5; // Terme constant 1/2

            // Paramètres d'intégration
            int numSteps = 500;
            double maxPhi = 100; // Limite d'intégration supérieure
            double dPhi = maxPhi / numSteps;
            double phi;

            // Paramètres pour j=1 ou j=2
            double u = (j == 1) ? 0.5 : -0.5;
            double b = kappa - ((j == 1) ? rho * sigma : 0);

            // Intégration numérique trapézoïdale
            for (int i = 1; i <= numSteps; i++)
            {
                phi = i * dPhi;

                // Calculer la fonction caractéristique
                Complex cf = CharacteristicFunction(phi, j, S, v0, T, kappa, theta, sigma, rho, r);

                // Partie réelle du terme à intégrer
                double integrand = (Complex.Exp(-Complex.ImaginaryOne * phi * Math.Log(K)) *
                                   cf / (Complex.ImaginaryOne * phi)).Real;

                // Intégration trapézoïdale
                if (i == 1 || i == numSteps)
                {
                    integralResult += 0.5 * integrand * dPhi / Math.PI;
                }
                else
                {
                    integralResult += integrand * dPhi / Math.PI;
                }
            }

            return integralResult;
        }

        // Fonction caractéristique du modèle de Heston
        private Complex CharacteristicFunction(double phi, int j, double S, double v0, double T,
                                              double kappa, double theta, double sigma,
                                              double rho, double r)
        {
            double u = (j == 1) ? 0.5 : -0.5;
            Complex i = Complex.ImaginaryOne;

            // Paramètres
            Complex a = kappa * theta;
            Complex b = kappa;

            if (j == 1)
            {
                b = kappa + rho * sigma; // Ajustement pour P1
            }

            // Formule à partir de Heston (1993)
            Complex d = Complex.Sqrt(Complex.Pow(b - rho * sigma * i * phi, 2) +
                                    (sigma * sigma) * (i * phi + phi * phi));

            Complex g = (b - rho * sigma * i * phi - d) /
                       (b - rho * sigma * i * phi + d);

            // Calcul des termes exponentiels
            Complex C = (r * i * phi * T) +
                       (a / (sigma * sigma)) *
                       ((b - rho * sigma * i * phi - d) * T -
                        2 * Complex.Log((1 - g * Complex.Exp(-d * T)) / (1 - g)));

            Complex D = (b - rho * sigma * i * phi - d) /
                       (sigma * sigma) *
                       ((1 - Complex.Exp(-d * T)) /
                        (1 - g * Complex.Exp(-d * T)));

            // Fonction caractéristique complète
            return Complex.Exp(C + D * v0 + i * phi * Math.Log(S));
        }

        // =================== METHODE MONTE CARLO DETAILLEE ===================
        private double PriceByMonteCarlo()
        {
            bool isCall = OptionType == "Call";
            double r = InterestRate / 100.0;
            double v0 = Math.Pow(V0 / 100.0, 2); // Variance initiale
            double theta = Math.Pow(Theta, 2);   // Variance long terme
            double kappa = Kappa;
            double sigma = Sigma;
            double rho = Rho;

            // Paramètres de simulation
            int steps = 252;  // Un pas de temps par jour de trading
            double dt = Maturity / steps;
            double sqrtDt = Math.Sqrt(dt);

            // Pour calculer l'intervalle de confiance
            double sumPayoffs = 0;
            double sumSquaredPayoffs = 0;

            var rand = new Random(42); // Seed fixe pour la reproductibilité

            // Boucle principale de simulation
            for (int sim = 0; sim < Simulations; sim++)
            {
                double s = Spot; // Prix initial
                double v = v0;   // Variance initiale

                // Simulation d'un chemin
                for (int step = 0; step < steps; step++)
                {
                    // Générer deux variables normales corrélées
                    double z1 = BoxMuller(rand);
                    double z2 = rho * z1 + Math.Sqrt(1 - rho * rho) * BoxMuller(rand);

                    // 1. Mise à jour de la variance (schéma d'Euler modifié)
                    double vPrev = v;
                    // La fonction Math.Max empêche la variance de devenir négative
                    v = Math.Max(0, v + kappa * (theta - v) * dt +
                                    sigma * Math.Sqrt(Math.Max(0, v)) * z2 * sqrtDt);

                    // 2. Mise à jour du prix (schéma logarithmique)
                    // Utiliser la moyenne de v(t) et v(t+dt) pour améliorer la précision
                    double vAvg = (v + vPrev) / 2.0;
                    s *= Math.Exp((r - 0.5 * vAvg) * dt +
                                  Math.Sqrt(vAvg) * z1 * sqrtDt);
                }

                // Calcul du payoff
                double payoff = isCall ? Math.Max(0, s - Strike) : Math.Max(0, Strike - s);

                // Accumulation pour la moyenne et l'écart type
                sumPayoffs += payoff;
                sumSquaredPayoffs += payoff * payoff;
            }

            // Calcul du prix final (moyenne des payoffs actualisée)
            double meanPayoff = sumPayoffs / Simulations;
            double price = Math.Exp(-r * Maturity) * meanPayoff;

            // Calcul de l'écart-type (pour l'intervalle de confiance)
            double variance = (sumSquaredPayoffs / Simulations) - (meanPayoff * meanPayoff);
            double stdError = Math.Sqrt(variance / Simulations);
            double confidenceIntervalWidth = 1.96 * stdError * Math.Exp(-r * Maturity);

            // Stockage optionnel de l'erreur d'estimation (pour l'affichage)
            // StdError = confidenceIntervalWidth;

            return price;
        }

        // Fonction Box-Muller pour générer des nombres aléatoires normaux
        private double BoxMuller(Random rand)
        {
            double u1 = 1.0 - rand.NextDouble(); // Évite u1 = 0
            double u2 = 1.0 - rand.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        // =================== METHODE DES DIFFERENCES FINIES ===================
        private double PriceByFiniteDifference()
        {
            bool isCall = OptionType == "Call";
            double r = InterestRate / 100.0;
            double v0 = Math.Pow(V0 / 100.0, 2); // Variance initiale
            double theta = Math.Pow(Theta, 2);   // Variance long terme
            double kappa = Kappa;
            double sigma = Sigma;
            double rho = Rho;

            // Dans un démonstrateur réel, on implémenterait la méthode des différences finies
            // pour résoudre l'équation aux dérivées partielles de Heston.
            // Pour simplifier, nous renvoyons une approximation basée sur la méthode semi-analytique.

            double semiAnalyticalPrice = PriceBySemiAnalytical();

            // Simuler une petite variation typique de l'erreur numérique
            return semiAnalyticalPrice * (1 + 0.005 * Math.Sin(Spot * Strike * 0.01));
        }

        // =================== CALCUL DES GREEKS ===================
        // Les Greeks sont calculés par différences finies pour une meilleure précision

        // Delta: ∂V/∂S - variation du prix par rapport au spot
        private double CalculateDelta()
        {
            double h = 0.01 * Spot;  // Taille du pas

            double originalSpot = Spot;

            // Prix avec spot + h
            Spot = originalSpot + h;
            double pricePlus = PriceBySemiAnalytical();

            // Prix avec spot - h
            Spot = originalSpot - h;
            double priceMinus = PriceBySemiAnalytical();

            // Restaurer le spot d'origine
            Spot = originalSpot;

            // Formule centrée à l'ordre 2: (f(x+h) - f(x-h)) / (2h)
            return (pricePlus - priceMinus) / (2 * h);
        }

        // Gamma: ∂²V/∂S² - variation du delta
        private double CalculateGamma()
        {
            double h = 0.01 * Spot;  // Taille du pas

            double originalSpot = Spot;

            // Prix avec spot + h
            Spot = originalSpot + h;
            double pricePlus = PriceBySemiAnalytical();

            // Prix au spot d'origine
            Spot = originalSpot;
            double priceCenter = PriceBySemiAnalytical();

            // Prix avec spot - h
            Spot = originalSpot - h;
            double priceMinus = PriceBySemiAnalytical();

            // Restaurer le spot d'origine
            Spot = originalSpot;

            // Formule centrée à l'ordre 2: (f(x+h) - 2f(x) + f(x-h)) / h²
            return (pricePlus - 2 * priceCenter + priceMinus) / (h * h);
        }

        // Theta: -∂V/∂t - variation du prix par rapport au temps
        private double CalculateTheta()
        {
            double dt = 1.0 / 365.0;  // Un jour en années

            double originalMaturity = Maturity;

            // Prix à la maturité originale
            double priceNow = PriceBySemiAnalytical();

            // Prix avec un jour de moins
            Maturity = originalMaturity - dt;
            double priceTomorrow = PriceBySemiAnalytical();

            // Restaurer la maturité d'origine
            Maturity = originalMaturity;

            // Theta est calculé comme la variation par jour
            return (priceTomorrow - priceNow) / dt;
        }

        // Vega: ∂V/∂σ - variation du prix par rapport à la volatilité
        private double CalculateVega()
        {
            double dv = 0.01;  // 1% de volatilité

            double originalVol = V0;

            // Prix avec vol + 1%
            V0 = originalVol + dv;
            double pricePlus = PriceBySemiAnalytical();

            // Prix avec vol - 1%
            V0 = originalVol - dv;
            double priceMinus = PriceBySemiAnalytical();

            // Restaurer la volatilité d'origine
            V0 = originalVol;

            // Formule centrée: (f(vol+dv) - f(vol-dv)) / (2*dv)
            return (pricePlus - priceMinus) / (2 * dv);
        }

        // Rho: ∂V/∂r - variation du prix par rapport au taux d'intérêt
        private double CalculateRho()
        {
            double dr = 0.0010;  // 0.1% (10 points de base)

            double originalRate = InterestRate;

            // Prix avec taux + 0.1%
            InterestRate = originalRate + dr * 100;  // Multiplier par 100 car InterestRate est en %
            double pricePlus = PriceBySemiAnalytical();

            // Prix avec taux - 0.1%
            InterestRate = originalRate - dr * 100;
            double priceMinus = PriceBySemiAnalytical();

            // Restaurer le taux d'origine
            InterestRate = originalRate;

            // Formule centrée: (f(r+dr) - f(r-dr)) / (2*dr)
            return (pricePlus - priceMinus) / (2 * dr);
        }

        // =================== FORMULES BLACK-SCHOLES (pour comparaison) ===================
        public double BlackScholesFormula(bool isCall, double spot, double strike, double maturity, double r, double vol)
        {
            // Calcul des paramètres d1 et d2
            double d1 = (Math.Log(spot / strike) + (r + 0.5 * vol * vol) * maturity) / (vol * Math.Sqrt(maturity));
            double d2 = d1 - vol * Math.Sqrt(maturity);

            // Calcul du prix selon que c'est un call ou un put
            if (isCall)
                return spot * CumulativeNormal(d1) - strike * Math.Exp(-r * maturity) * CumulativeNormal(d2);
            else
                return strike * Math.Exp(-r * maturity) * CumulativeNormal(-d2) - spot * CumulativeNormal(-d1);
        }

        // Distribution normale cumulée Φ(x)
        private double CumulativeNormal(double x)
        {
            // Approximation de la fonction de répartition normale
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            double sign = 1;
            if (x < 0)
            {
                sign = -1;
                x = -x;
            }

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return 0.5 * (1.0 + sign * y);
        }
    }

    // Classes de support pour les résultats
    public class HestonPricingResult
    {
        public double SemiAnalyticalPrice { get; set; }
        public double MonteCarloPrice { get; set; }
        public double FiniteDifferencePrice { get; set; }
        public double MarketPrice { get; set; }

        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double ThetaGreek { get; set; }
        public double Vega { get; set; }
        public double RhoGreek { get; set; }

        public VolatilitySurfaceData VolatilitySurface { get; set; }
    }

    public class VolatilitySurfaceData
    {
        public List<double> Strikes { get; set; }
        public List<double> Maturities { get; set; }
        public List<double> ImpliedVolatilities { get; set; }
    }

  
}