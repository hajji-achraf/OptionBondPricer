# 📊 Finance Quantitative - Valorisation d'Options et Obligations

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Licence](https://img.shields.io/badge/licence-MIT-green.svg)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-7.0-purple.svg)
![C#](https://img.shields.io/badge/C%23-10.0-brightgreen.svg)

Une application web interactive pour la valorisation d'instruments financiers dérivés, implémentant des modèles mathématiques avancés comme Black-Scholes, Heston, Vasicek et Hull-White.

## 📑 Table des matières

- [Aperçu du projet](#aperçu-du-projet)
- [Modèles implémentés](#modèles-implémentés)
- [Fonctionnalités](#fonctionnalités)
- [Architecture technique](#architecture-technique)
- [Installation](#installation)
- [Utilisation](#utilisation)
- [Captures d'écran](#captures-décran)
- [Technologies utilisées](#technologies-utilisées)
- [Auteur](#auteur)

## 🔍 Aperçu du projet

Cette application web interactive développée en C# et ASP.NET permet aux utilisateurs de valoriser des options et des obligations en utilisant différents modèles mathématiques stochastiques. L'interface intuitive offre une expérience pédagogique et professionnelle à l'interface entre théorie financière et application pratique.

Le projet constitue une implémentation concrète des concepts théoriques de finance quantitative, permettant de visualiser en temps réel l'impact des différents paramètres sur les prix et les trajectoires des actifs financiers.

## 📐 Modèles implémentés

### Valorisation d'options
1. **Modèle de Black-Scholes**
   - Formule fermée pour les options européennes
   - Calcul des grecques (delta, gamma, vega, theta, rho)
   - Visualisation de la surface de volatilité implicite

2. **Modèle de Heston**
   - Simulation Monte Carlo avec volatilité stochastique
   - Calibration sur les données du marché
   - Prise en compte du smile de volatilité

### Valorisation d'obligations
1. **Modèle de Vasicek**
   - Modélisation des taux d'intérêt avec retour à la moyenne
   - Calcul des prix d'obligations zéro-coupon et à coupon
   - Simulation de trajectoires de taux

2. **Modèle de Hull-White**
   - Extension du modèle de Vasicek avec θ(t) dépendant du temps
   - Paramétrage des fonctions θ(t) (constante, linéaire, exponentielle, sinusoïdale)
   - Visualisation des surfaces de taux dans le temps

## ✨ Fonctionnalités

- **Interface interactive** pour paramétrer librement les modèles
- **Visualisations dynamiques** (graphiques 2D et surfaces 3D)
- **Simulations Monte Carlo** pour les trajectoires de prix et de taux
- **Calcul en temps réel** des prix et des indicateurs statistiques
- **Mode sombre/clair** pour une expérience utilisateur optimale
- **Design responsive** adapté aux différents appareils
- **Exportation des résultats** en format CSV ou JSON

## 🏗️ Architecture technique

L'application est construite selon le modèle MVC (Model-View-Controller) avec une séparation claire des responsabilités :
