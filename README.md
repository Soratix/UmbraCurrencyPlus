# Umbra – Monnaies+

**Français** · [English](README.en.md)

Plugin pour [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) qui ajoute le widget **« Monnaies+ »** (« Currencies+ » en anglais) : une icône dans la barre d'outils qui ouvre vos monnaies et ce que vous pouvez en faire. Il remplace le plugin Dalamud [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender).

## Fonctionnalités

- **Dans la barre :** une simple icône (personnalisable). Elle prend la couleur d'alerte quand une monnaie suivie dépasse un seuil de son plafond (90 % par défaut). L'infobulle liste les monnaies suivies, avec quantité et plafond.
- **Clic sur l'icône :** vos monnaies suivies en haut de la fenêtre. Cliquez sur l'une d'elles pour voir quoi en faire, dans deux onglets. Un onglet n'apparaît que s'il y a quelque chose dedans.
  - **Rentable :** les objets revendables au tableau des ventes, avec leur coût, leur prix de vente (médiane des ventes des 7 derniers jours), le nombre de ventes sur 7 jours, les gils par point et un score (gils par point × ventes sur 7 jours). Le tri par défaut se fait sur le score : un objet cher que personne n'achète ne passe pas devant. Cliquez sur un en-tête pour trier sur une autre colonne. Les colonnes affichées se choisissent dans les réglages du widget.
  - **Collection :** les objets à débloquer (montures, mascottes, orchestrions, emotes, coiffures, cartes de Triple Triad, bardes…) que vous n'avez ni débloqués, ni déjà dans votre inventaire ou vos sacoches de chocobo, avec leur type, leur coût et si vous avez assez de monnaie.
- **Clic sur un objet :** un drapeau est posé sur le vendeur, le lien de l'objet et de l'emplacement s'affiche dans votre chat, et vous êtes téléporté à l'éthérite débloquée la plus proche du vendeur. Si plusieurs PNJ le vendent, celui de votre zone est choisi, sinon celui dont la téléportation coûte le moins. La téléportation peut être désactivée dans les réglages (drapeau seul).
- **Bouton « ⚙ Gérer » :** toutes les monnaies de la fenêtre « Devises » du jeu, avec leur icône, rangées par groupe (mémoquartz, Grande Compagnie, combat, artisanat et récolte, tribus alliées, autres). Cliquez sur une monnaie pour la suivre ou ne plus la suivre.
- **Bouton « Rafraîchir » :** redemande les prix sans attendre la fin du cache.

L'interface du widget, nom compris, suit la langue d'Umbra : en français si Umbra est en français, en anglais sinon. Les noms d'objets, de monnaies et de PNJ suivent la langue du client.

## Prix et réseau

- Les prix viennent de l'[API Universalis](https://docs.universalis.app/), pour votre monde d'origine ou, en option, tout son centre de données.
- Aucune requête tant que la fenêtre est fermée. Les prix d'une monnaie sont gardés en cache 3 heures (réglable).
- Les objets sont demandés par lots de 100 (la limite d'Universalis), avec au plus 2 requêtes simultanées.
- L'onglet « Collection » ne fait aucun appel réseau : l'état des déblocages est lu dans le jeu.

## Installation

1. Dans le jeu, ouvrez les **paramètres d'Umbra → Plugins**.
2. Ajoutez un dépôt GitHub : propriétaire `Soratix`, dépôt `UmbraCurrencyPlus`.
3. Passez la barre d'outils en mode édition et ajoutez le widget **« Monnaies+ »** (« Currencies+ » si Umbra n'est pas en français). Dans la recherche, vous pouvez aussi taper `monnaie`, `currency` ou `universalis`.
4. Cliquez sur l'icône, puis sur **« ⚙ Gérer »** pour choisir vos monnaies.

## Compiler soi-même

Il faut le SDK .NET 10 et XIVLauncher avec Umbra installé : les références pointent vers `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release CurrencyPlus\CurrencyPlus.csproj
```

## Limites connues

- **Monnaies non couvertes :** les gils (boutiques classiques), les crédits de compagnie libre et les crédits de loterie d'Orbitingway (objets aléatoires).
- **Boutiques :** la liste est lue dans toutes les boutiques du jeu (SpecialShop, boutiques de Grande Compagnie). Les boutiques d'événements saisonniers sont exclues, mais un objet peut apparaître même si sa boutique n'est pas encore débloquée pour vous (rang de Grande Compagnie, réputation de tribu, quête…).
- **Emplacement des vendeurs :** il vient des données du jeu. Il est connu pour la quasi-totalité des objets des monnaies courantes, mais pas pour quelques anciennes boutiques : dans ce cas, seul le lien de l'objet s'affiche.
- **Objets déjà possédés :** l'inventaire et les sacoches de chocobo sont vérifiés, pas les servants.
- **Échanges à plusieurs monnaies :** un objet qui coûte plusieurs monnaies différentes à la fois est ignoré.
- **Prix :** ce sont des ventes passées, pas les annonces en cours. Les données d'Universalis dépendent des joueurs qui les envoient : un objet peu échangé peut avoir un prix approximatif ou absent.

## Crédits

La conversion des identifiants de monnaie des boutiques, la liste des boutiques d'événement et le rattachement des boutiques aux PNJ s'inspirent de [CurrencySpender](https://github.com/Blackcatz1911/CurrencySpender) (Blackcatz1911, AGPL-3.0).

## Licence

AGPL-3.0, comme Umbra.
