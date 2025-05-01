using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace App.Models
{
    public class HullWhiteModel
    {
        // Paramètres du modèle de Hull-White
        [Display(Name = "Taux d'intérêt initial (r0)")]
        public double InitialRate { get; set; } = 0.03;

        [Display(Name = "Vitesse de retour à la moyenne (α)")]
        public double MeanReversionSpeed { get; set; } = 0.1;

        [Display(Name = "Volatilité (σ)")]
        public double Volatility { get; set; } = 0.02;

        // Types de fonction theta
        public enum ThetaFunctionType
        {
            [Display(Name = "Constante")]
            Constant,
            [Display(Name = "Linéaire")]
            Linear,
            [Display(Name = "Exponentielle")]
            Exponential,
            [Display(Name = "Sinusoïdale")]
            Sinusoidal,
            [Display(Name = "Personnalisée")]
            Custom
        }

        [Display(Name = "Type de fonction θ(t)")]
        public ThetaFunctionType ThetaType { get; set; } = ThetaFunctionType.Constant;

        // Paramètres pour les fonctions theta prédéfinies
        [Display(Name = "Valeur initiale de θ")]
        public double ThetaInitial { get; set; } = 0.05;

        [Display(Name = "Valeur finale de θ (pour fonction linéaire)")]
        public double ThetaFinal { get; set; } = 0.07;

        [Display(Name = "Période (pour fonction sinusoïdale, en années)")]
        public double ThetaPeriod { get; set; } = 5.0;

        [Display(Name = "Amplitude (pour fonction sinusoïdale/exponentielle)")]
        public double ThetaAmplitude { get; set; } = 0.02;

        [Display(Name = "Expression personnalisée pour θ(t)")]
        public string CustomThetaExpression { get; set; } = "0.05 + 0.01 * Math.Sin(t)";

        // Paramètres de l'obligation
        [Display(Name = "Valeur nominale")]
        public double FaceValue { get; set; } = 1000;

        [Display(Name = "Taux du coupon")]
        public double CouponRate { get; set; } = 0.04;

        [Display(Name = "Fréquence des coupons par an")]
        public int CouponFrequency { get; set; } = 2;

        [Display(Name = "Maturité (années)")]
        public double Maturity { get; set; } = 10;

        // Résultats
        public double BondPrice { get; set; }
        public double Duration { get; set; }
        public double YieldToMaturity { get; set; }

        // Données de simulation
        public List<double> TimePoints { get; set; } = new List<double>();
        public List<double> RateValues { get; set; } = new List<double>();
        public List<double> PriceValues { get; set; } = new List<double>();

        // Points pour tracer la fonction theta
        public List<double> ThetaValues { get; set; } = new List<double>();

        // Calcul de theta au temps t
        public double CalculateTheta(double t)
        {
            switch (ThetaType)
            {
                case ThetaFunctionType.Constant:
                    return ThetaInitial;

                case ThetaFunctionType.Linear:
                    // Interpolation linéaire entre ThetaInitial et ThetaFinal sur [0, Maturity]
                    return ThetaInitial + (ThetaFinal - ThetaInitial) * (t / Maturity);

                case ThetaFunctionType.Exponential:
                    // Croissance/décroissance exponentielle
                    return ThetaInitial * Math.Exp(ThetaAmplitude * t);

                case ThetaFunctionType.Sinusoidal:
                    // Fonction sinusoïdale avec une période spécifiée
                    return ThetaInitial + ThetaAmplitude * Math.Sin((2 * Math.PI * t) / ThetaPeriod);

                case ThetaFunctionType.Custom:
                    // Pour la version simplifiée, nous allons utiliser quelques fonctions prédéfinies
                    // Dans une version plus avancée, on pourrait utiliser un interpréteur d'expressions
                    try
                    {
                        // Exemple simple: interpréter t comme variable
                        // Note: ceci est une implémentation très basique et non sécurisée
                        if (CustomThetaExpression.Contains("Math.Sin"))
                            return 0.05 + 0.01 * Math.Sin(t);
                        if (CustomThetaExpression.Contains("Math.Exp"))
                            return 0.05 * Math.Exp(0.1 * t);
                        if (CustomThetaExpression.Contains("*"))
                            return 0.05 + 0.005 * t;

                        // Par défaut
                        return ThetaInitial;
                    }
                    catch
                    {
                        // En cas d'erreur, retourner la valeur initiale
                        return ThetaInitial;
                    }

                default:
                    return ThetaInitial;
            }
        }

        // Calcul du prix d'une obligation zéro-coupon selon Hull-White
        public double CalculateZeroBondPrice(double t, double T)
        {
            // Dans Hull-White, le calcul est plus complexe car theta dépend du temps
            // Ceci est une version simplifiée pour la démonstration
            double B = (1 - Math.Exp(-MeanReversionSpeed * (T - t))) / MeanReversionSpeed;

            // Pour Hull-White, nous utilisons la moyenne de theta sur l'intervalle [t,T]
            double avgTheta = 0;
            int steps = 20; // Nombre de pas pour l'intégration numérique
            double dt = (T - t) / steps;

            for (int i = 0; i < steps; i++)
            {
                double ti = t + i * dt;
                avgTheta += CalculateTheta(ti) * dt;
            }
            avgTheta /= (T - t);

            // Formule simplifiée adaptée de Hull-White
            double A = Math.Exp((avgTheta - Volatility * Volatility / (2 * MeanReversionSpeed * MeanReversionSpeed)) *
                      (B - (T - t)) - (Volatility * Volatility * B * B) / (4 * MeanReversionSpeed));

            return A * Math.Exp(-B * InitialRate);
        }

        // Calcul du prix de l'obligation
        public void CalculateBondPrice()
        {
            // Calcul du prix
            double price = 0;
            double couponPayment = FaceValue * CouponRate / CouponFrequency;

            for (int i = 1; i <= Maturity * CouponFrequency; i++)
            {
                double timeToPayment = i * (1.0 / CouponFrequency);
                price += couponPayment * CalculateZeroBondPrice(0, timeToPayment);
            }

            // Ajouter le remboursement du principal
            price += FaceValue * CalculateZeroBondPrice(0, Maturity);

            BondPrice = price;

            // Calcul du YTM (simplifié)
            YieldToMaturity = CouponRate + (FaceValue - BondPrice) / (BondPrice * Maturity);

            // Calcul de la duration (simplifié)
            Duration = Maturity / (1 + YieldToMaturity);

            // Générer les valeurs de theta pour le graphique
            GenerateThetaValues();

            // Générer les simulations
            GenerateSimulations();
        }

        // Génération des valeurs de theta pour le graphique
        private void GenerateThetaValues()
        {
            ThetaValues.Clear();
            int steps = 100;
            double dt = Maturity / steps;

            for (int i = 0; i <= steps; i++)
            {
                double t = i * dt;
                ThetaValues.Add(CalculateTheta(t));
            }
        }

        // Génération des simulations pour les graphiques
        private void GenerateSimulations()
        {
            // Effacer les anciennes données
            TimePoints.Clear();
            RateValues.Clear();
            PriceValues.Clear();

            // Paramètres de simulation
            Random random = new Random();
            double currentRate = InitialRate;
            double dt = 0.1;
            int steps = (int)(Maturity / dt) + 1;

            for (int i = 0; i < steps; i++)
            {
                double t = i * dt;
                TimePoints.Add(t);

                // Simulation du taux selon Hull-White
                if (i > 0) // Le premier point est le taux initial
                {
                    // La différence principale avec Vasicek: theta dépend du temps
                    double thetaT = CalculateTheta(t);
                    double meanReversion = MeanReversionSpeed * (thetaT - currentRate) * dt;
                    double randomShock = Volatility * Math.Sqrt(dt) * (random.NextDouble() * 2 - 1);
                    currentRate += meanReversion + randomShock;
                    if (currentRate < 0.001) currentRate = 0.001; // Éviter les taux négatifs
                }

                RateValues.Add(currentRate);

                // Calcul du prix à ce point de temps
                double price = 0;
                double remainingTime = Maturity - t;

                if (remainingTime <= 0)
                {
                    price = FaceValue; // À maturité
                }
                else
                {
                    // Prix simplifié basé sur le taux actuel
                    double discountFactor = Math.Exp(-currentRate * remainingTime);
                    price = FaceValue * discountFactor * (1 + CouponRate * remainingTime);
                }

                PriceValues.Add(price);
            }
        }
    }
}