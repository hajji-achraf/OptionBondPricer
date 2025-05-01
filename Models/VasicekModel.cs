using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace App.Models
{
    public class VasicekModel
    {
        // Paramètres du modèle de Vasicek
        [Display(Name = "Taux d'intérêt initial (r0)")]
        public double InitialRate { get; set; } = 0.03;

        [Display(Name = "Vitesse de retour à la moyenne (κ)")]
        public double MeanReversionSpeed { get; set; } = 0.1;

        [Display(Name = "Taux d'intérêt à long terme (θ)")]
        public double LongTermRate { get; set; } = 0.05;

        [Display(Name = "Volatilité (σ)")]
        public double Volatility { get; set; } = 0.02;

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

        // Calcul du prix d'une obligation zéro-coupon
        public double CalculateZeroBondPrice(double t, double T)
        {
            double B = (1 - Math.Exp(-MeanReversionSpeed * (T - t))) / MeanReversionSpeed;
            double A = Math.Exp((LongTermRate - Volatility * Volatility / (2 * MeanReversionSpeed * MeanReversionSpeed)) *
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

            // Générer les simulations
            GenerateSimulations();
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

                // Simulation du taux selon Vasicek
                if (i > 0) // Le premier point est le taux initial
                {
                    double meanReversion = MeanReversionSpeed * (LongTermRate - currentRate) * dt;
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