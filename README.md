# Umbra – Monnaies+

**Français** · [English](README.en.md)

Plugin pour [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) qui ajoute le widget **« Monnaies+ »** (« Currencies+ » en anglais) : vos monnaies dans la barre d'outils et, d'un clic, ce que vous pouvez en faire. Il remplace le plugin Dalamud [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender).

## Fonctionnalités

- **Dans la barre :** une ou plusieurs monnaies, avec leur icône et leur quantité, comme le widget « Monnaies » d'Umbra. La quantité change de couleur au-delà d'un seuil de son plafond (90 % par défaut).
- **Infobulle :** toutes les monnaies suivies, avec quantité et plafond.
- **Clic sur une monnaie :** une fenêtre avec deux onglets. La monnaie cliquée est présélectionnée, et une rangée en haut permet de passer à une autre.
  - **Rentable :** les objets revendables au tableau des ventes, avec leur coût, leur prix de vente (médiane des ventes des 7 derniers jours), le nombre de ventes sur 7 jours, les gils par point et un score (gils par point × ventes sur 7 jours). Le tri par défaut se fait sur le score : un objet cher que personne n'achète ne passe pas devant. Cliquez sur un en-tête pour trier sur une autre colonne.
  - **Collection :** les objets à débloquer que votre personnage n'a pas encore (montures, mascottes, orchestrions, emotes, coiffures, cartes de Triple Triad, bardes…), avec leur type, leur coût et si vous avez assez de monnaie.
- **Clic sur un objet :** son lien s'affiche dans votre chat (visible par vous seul, cliquable) et son nom est copié dans le presse-papiers, prêt à coller dans la recherche du tableau des ventes.
- **Bouton « Rafraîchir » :** redemande les prix sans attendre la fin du cache.

Monnaies proposées : mémoquartz (poétique, non limité et limité de la saison), sceaux de votre Grande Compagnie, insignes alliés, Centurio et de chasse, marques de loup, cristaux-trophées, assignats mauves et orange (artisan et récolteur), assignats d'Azurée, gemmes bicolores et PGS. Vous pouvez ajouter n'importe quelle autre monnaie par son ID d'objet (par exemple une monnaie de tribu alliée).

L'interface du widget, nom compris, suit la langue d'Umbra : en français si Umbra est en français, en anglais sinon. Les noms d'objets et de monnaies suivent la langue du client.

## Prix et réseau

- Les prix viennent de l'[API Universalis](https://docs.universalis.app/), pour votre monde d'origine ou, en option, tout son centre de données.
- Aucune requête tant que la fenêtre est fermée. Les prix d'une monnaie sont gardés en cache 3 heures (réglable).
- Les objets sont demandés par lots de 100 (la limite d'Universalis), avec au plus 2 requêtes simultanées.
- L'onglet « Collection » ne fait aucun appel réseau : l'état des déblocages est lu dans le jeu.

## Installation

1. Dans le jeu, ouvrez les **paramètres d'Umbra → Plugins**.
2. Ajoutez un dépôt GitHub : propriétaire `Soratix`, dépôt `UmbraCurrencyPlus`.
3. Passez la barre d'outils en mode édition et ajoutez le widget **« Monnaies+ »** (« Currencies+ » si Umbra n'est pas en français). Dans la recherche, vous pouvez aussi taper `monnaie`, `currency` ou `universalis`.
4. Choisissez vos monnaies dans l'onglet **« Monnaies suivies »** des réglages du widget.

## Compiler soi-même

Il faut le SDK .NET 10 et XIVLauncher avec Umbra installé : les références pointent vers `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release CurrencyPlus\CurrencyPlus.csproj
```

## Limites connues

- **Monnaies non couvertes :** les gils (boutiques classiques), les crédits de compagnie libre et les crédits de loterie d'Orbitingway (objets aléatoires).
- **Boutiques :** la liste est lue dans toutes les boutiques du jeu (SpecialShop, boutiques de Grande Compagnie), pas PNJ par PNJ. Les boutiques d'événements saisonniers sont exclues, mais un objet peut apparaître même si sa boutique n'est pas encore débloquée pour vous (rang de Grande Compagnie, réputation de tribu, quête…).
- **Échanges à plusieurs monnaies :** un objet qui coûte plusieurs monnaies différentes à la fois est ignoré.
- **Prix :** ce sont des ventes passées, pas les annonces en cours. Les données d'Universalis dépendent des joueurs qui les envoient : un objet peu échangé peut avoir un prix approximatif ou absent.

## Crédits

La conversion des identifiants de monnaie des boutiques et la liste des boutiques d'événement viennent de [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) (Blackcatz1911, AGPL-3.0).

## Licence

AGPL-3.0, comme Umbra.
