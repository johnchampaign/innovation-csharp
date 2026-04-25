using Innovation.Core.Handlers;

namespace Innovation.Core;

/// <summary>
/// Registers the hand-ported dogma definitions on a <see cref="CardRegistry"/>.
/// Cards are added here incrementally as their effects are implemented;
/// anything not registered falls back to <see cref="PlaceholderHandler"/>
/// so the game still runs.
///
/// Keyed by card <em>title</em> rather than ID so this code doesn't depend
/// on the TSV's ordering — the catalog is the source of truth for what
/// "Writing" is.
///
/// <para><b>Handler authoring rules</b> (see <see cref="Mechanics"/> for
/// the rationale):</para>
/// <list type="bullet">
///   <item><b>Always open <c>Data/cards.tsv</c> first.</b> Its last two
///         columns (demand text, non-demand text) are the authoritative
///         rule text — do not implement from memory or from a similar
///         card's behavior. Guessing at text has already caused bugs
///         (e.g. an early Construction port missed the "draw a 2"
///         consolation).</item>
///   <item>Handlers compose <c>Mechanics.*</c> calls. They never invoke
///         another handler, and never go through
///         <see cref="TurnManager.Apply"/>. Action cost is the action
///         layer's business; mid-dogma effects are free.</item>
///   <item>Shared behavior (e.g. "draw and score an N", "transfer a
///         card from X to Y") lives as a helper on <see cref="Mechanics"/>.
///         Handlers just orchestrate choice and composition.</item>
/// </list>
/// </summary>
public static class CardRegistrations
{
    /// <summary>
    /// Add every currently-implemented card's dogma to <paramref name="r"/>.
    /// </summary>
    public static void RegisterAll(CardRegistry r, IReadOnlyList<Card> cards)
    {
        // --- Age 1 ---

        // Writing (Blue, Lightbulb): "Draw a 2."
        Register(r, cards, "Writing", Icon.Lightbulb, isDemand: false,
            text: "Draw a 2.",
            handler: new DrawHandler(count: 1, startingAge: 2));

        // The Wheel (Green, Castle): "Draw two 1s."
        Register(r, cards, "The Wheel", Icon.Castle, isDemand: false,
            text: "Draw two 1s.",
            handler: new DrawHandler(count: 2, startingAge: 1));

        // Sailing (Green, Crown): "Draw and meld a 1."
        Register(r, cards, "Sailing", Icon.Crown, isDemand: false,
            text: "Draw and meld a 1.",
            handler: new DrawAndMeldHandler(count: 1, startingAge: 1));

        // Mysticism (Purple, Castle): "Draw a 1. If it is the same color as
        // any card on your board, meld it and draw a 1."
        Register(r, cards, "Mysticism", Icon.Castle, isDemand: false,
            text: "Draw a 1. If it is the same color as any card on your board, meld it and draw a 1.",
            handler: new MysticismHandler());

        // Metalworking (Red, Castle): "Draw and reveal a 1. If it has a
        // [Castle], score it and repeat this dogma effect. Otherwise, keep it."
        Register(r, cards, "Metalworking", Icon.Castle, isDemand: false,
            text: "Draw and reveal a 1. If it has a [Castle], score it and repeat this dogma effect. Otherwise, keep it.",
            handler: new MetalworkingHandler());

        // Domestication (Yellow, Castle): "Meld the lowest card in your hand.
        // Draw a 1."
        Register(r, cards, "Domestication", Icon.Castle, isDemand: false,
            text: "Meld the lowest card in your hand. Draw a 1.",
            handler: new DomesticationHandler());

        // Agriculture (Yellow, Leaf): "You may return a card from your hand.
        // If you do, draw and score a card of value one higher than the card
        // you returned."
        Register(r, cards, "Agriculture", Icon.Leaf, isDemand: false,
            text: "You may return a card from your hand. If you do, draw and score a card of value one higher than the card you returned.",
            handler: new AgricultureHandler());

        // Masonry (Yellow, Castle): "You may meld any number of cards from
        // your hand, each with a [Castle]. If you melded four or more cards,
        // claim the Monument achievement."
        Register(r, cards, "Masonry", Icon.Castle, isDemand: false,
            text: "You may meld any number of cards from your hand, each with a [Castle]. If you melded four or more cards, claim the Monument achievement.",
            handler: new MasonryHandler());

        // Code of Laws (Purple, Crown): "You may tuck a card from your hand
        // of the same color as any card on your board. If you do, you may
        // splay that color of your cards left."
        Register(r, cards, "Code of Laws", Icon.Crown, isDemand: false,
            text: "You may tuck a card from your hand of the same color as any card on your board. If you do, you may splay that color of your cards left.",
            handler: new CodeOfLawsHandler());

        // Archery (Red, Castle, DEMAND): "I demand you draw a 1, then
        // transfer the highest card in your hand to my hand!"
        Register(r, cards, "Archery", Icon.Castle, isDemand: true,
            text: "I demand you draw a 1, then transfer the highest card in your hand to my hand!",
            handler: new ArcheryHandler());

        // Oars (Red, Castle): TWO effects —
        //   1. DEMAND: "Transfer a [Crown] card from your hand to my score
        //      pile. If you do, draw a 1."  (target draws)
        //   2. NON-DEMAND: "If no cards were transferred due to this demand,
        //      draw a 1."  (cross-effect state via DogmaContext.DemandSuccessful)
        RegisterMulti(r, cards, "Oars", Icon.Castle,
            new DogmaEffect(true,
                "I demand you transfer a card with a [Crown] from your hand to my score pile! If you do, draw a 1.",
                new OarsDemandHandler()),
            new DogmaEffect(false,
                "If no cards were transferred due to this demand, draw a 1.",
                new OarsDrawIfNoDemandHandler()));

        // City States (Purple, Crown, DEMAND): "Transfer a top Castle card
        // from your board to mine if you have ≥4 Castle icons. If you do,
        // draw a 1."
        Register(r, cards, "City States", Icon.Crown, isDemand: true,
            text: "I demand you transfer a top card with a [Castle] from your board to my board if you have at least four [Castle] icons on your board! If you do, draw a 1.",
            handler: new CityStatesHandler());

        // Tools (Blue, Lightbulb): TWO effects —
        //   1. "You may return three cards from your hand. If you do, draw
        //      and meld a 3."
        //   2. "You may return a 3 from your hand. If you do, draw three 1s."
        RegisterMulti(r, cards, "Tools", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may return three cards from your hand. If you do, draw and meld a 3.",
                new ToolsReturnThreeForMeldHandler()),
            new DogmaEffect(false,
                "You may return a 3 from your hand. If you do, draw three 1s.",
                new ToolsReturnThreeForOnesHandler()));

        // Clothing (Green, Leaf — top is blank, dogma is Leaf): TWO effects —
        //   1. "Meld a card from your hand of different color from any card
        //      on your board." (mandatory if eligible)
        //   2. "Draw and score a 1 for every color present on your board
        //      not present on any other player's board."
        RegisterMulti(r, cards, "Clothing", Icon.Leaf,
            new DogmaEffect(false,
                "Meld a card from your hand of different color from any card on your board.",
                new ClothingMeldDifferentColorHandler()),
            new DogmaEffect(false,
                "Draw and score a 1 for every color present on your board not present on any other player's board.",
                new ClothingDrawAndScoreUniqueColorsHandler()));

        // Pottery (Blue, Leaf): TWO effects —
        //   1. "You may return up to three cards from your hand. If you
        //      returned any cards, draw and score a card of value equal to
        //      the number of cards you returned."
        //   2. "Draw a 1."
        RegisterMulti(r, cards, "Pottery", Icon.Leaf,
            new DogmaEffect(false,
                "You may return up to three cards from your hand. If you returned any cards, draw and score a card of value equal to the number of cards you returned.",
                new PotteryReturnAndScoreHandler()),
            new DogmaEffect(false,
                "Draw a 1.",
                new DrawHandler(count: 1, startingAge: 1)));

        // --- Age 2 ---

        // Calendar (Blue, Leaf): "If you have more cards in your score pile
        // than in your hand, draw two 3s."
        Register(r, cards, "Calendar", Icon.Leaf, isDemand: false,
            text: "If you have more cards in your score pile than in your hand, draw two 3s.",
            handler: new CalendarHandler());

        // Fermenting (Yellow, Leaf): "Draw a 2 for every two [Leaf] icons
        // on your board."
        Register(r, cards, "Fermenting", Icon.Leaf, isDemand: false,
            text: "Draw a 2 for every two [Leaf] icons on your board.",
            handler: new FermentingHandler());

        // Mathematics (Blue, Lightbulb): "You may return a card from your
        // hand. If you do, draw and meld a card of value one higher than
        // the card you returned."
        Register(r, cards, "Mathematics", Icon.Lightbulb, isDemand: false,
            text: "You may return a card from your hand. If you do, draw and meld a card of value one higher than the card you returned.",
            handler: new MathematicsHandler());

        // Philosophy (Purple, Lightbulb): TWO effects —
        //   1. "You may splay left any one color of your cards."
        //   2. "You may score a card from your hand."
        RegisterMulti(r, cards, "Philosophy", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may splay left any one color of your cards.",
                new PhilosophySplayLeftHandler()),
            new DogmaEffect(false,
                "You may score a card from your hand.",
                new PhilosophyScoreHandler()));

        // Currency (Green, Crown): "You may return any number of cards from
        // your hand. If you do, draw a 2 for every different value of card
        // you returned."
        Register(r, cards, "Currency", Icon.Crown, isDemand: false,
            text: "You may return any number of cards from your hand. If you do, draw a 2 for every different value of card you returned.",
            handler: new CurrencyHandler());

        // Canal Building (Yellow, Crown): "You may exchange all the highest
        // cards in your hand with all the highest cards in your score pile."
        Register(r, cards, "Canal Building", Icon.Crown, isDemand: false,
            text: "You may exchange all the highest cards in your hand with all the highest cards in your score pile.",
            handler: new CanalBuildingHandler());

        // Mapmaking (Yellow, Crown): TWO effects —
        //   1. DEMAND: "Transfer a 1 from your score pile to my score pile."
        //   2. NON-DEMAND: "If any card was transferred due to the demand,
        //      draw and score a 1."
        RegisterMulti(r, cards, "Mapmaking", Icon.Crown,
            new DogmaEffect(true,
                "I demand you transfer a 1 from your score pile to my score pile!",
                new MapmakingDemandHandler()),
            new DogmaEffect(false,
                "If any card was transferred due to the demand, draw and score a 1.",
                new MapmakingDrawIfDemandHandler()));

        // Monotheism (Purple, Castle): TWO effects —
        //   1. DEMAND: "Transfer a top card of a color I don't have to my
        //      score pile. If you do, I draw and tuck a 1."
        //   2. NON-DEMAND: "Draw and tuck a 1."
        RegisterMulti(r, cards, "Monotheism", Icon.Castle,
            new DogmaEffect(true,
                "I demand you transfer a top card on your board of a color I do not have to my score pile! If you do, draw and tuck a 1!",
                new MonotheismDemandHandler()),
            new DogmaEffect(false,
                "Draw and tuck a 1.",
                new DrawAndTuckHandler(count: 1, startingAge: 1)));

        // Construction (Red, Castle): TWO effects —
        //   1. DEMAND: "Transfer two cards from your hand to my hand, then
        //      draw a 2." (target draws the 2; VB6 skipped this draw.)
        //   2. NON-DEMAND: "If you are the only player with five top cards,
        //      claim Empire."
        RegisterMulti(r, cards, "Construction", Icon.Castle,
            new DogmaEffect(true,
                "I demand you transfer two cards from your hand to my hand, then draw a 2!",
                new ConstructionDemandHandler()),
            new DogmaEffect(false,
                "If you are the only player with five top cards, claim the Empire achievement.",
                new ConstructionEmpireHandler()));

        // Road Building (Red, Castle): "Meld one or two cards from your hand.
        // If you melded two, you may transfer your top red card to another
        // player's board. In exchange, transfer that player's top green
        // card to your board."
        Register(r, cards, "Road Building", Icon.Castle, isDemand: false,
            text: "Meld one or two cards from your hand. If you melded two, you may transfer your top red card to another player's board. In exchange, transfer that player's top green card to your board.",
            handler: new RoadBuildingHandler());

        // --- Age 3 ---

        // Paper (Green, Lightbulb): TWO effects —
        //   1. "You may splay your green or blue cards left."
        //   2. "Draw a 4 for every color you have splayed left."
        RegisterMulti(r, cards, "Paper", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may splay your green or blue cards left.",
                new PaperSplayHandler()),
            new DogmaEffect(false,
                "Draw a 4 for every color you have splayed left.",
                new PaperDrawPerSplayHandler()));

        // Feudalism (Purple, Castle): TWO effects —
        //   1. DEMAND: "Transfer a card with a [Castle] from your hand to my hand."
        //   2. NON-DEMAND: "You may splay your yellow or purple cards left."
        RegisterMulti(r, cards, "Feudalism", Icon.Castle,
            new DogmaEffect(true,
                "I demand you transfer a card with a [Castle] from your hand to my hand!",
                new FeudalismDemandHandler()),
            new DogmaEffect(false,
                "You may splay your yellow or purple cards left.",
                new FeudalismSplayHandler()));

        // Engineering (Red, Castle): TWO effects —
        //   1. DEMAND: "Transfer all top cards with a [Castle] from your
        //      board to my score pile."
        //   2. NON-DEMAND: "You may splay your red cards left."
        RegisterMulti(r, cards, "Engineering", Icon.Castle,
            new DogmaEffect(true,
                "I demand you transfer all top cards with a [Castle] from your board to my score pile!",
                new EngineeringDemandHandler()),
            new DogmaEffect(false,
                "You may splay your red cards left.",
                new EngineeringSplayHandler()));

        // Education (Purple, Lightbulb): "You may return the highest card
        // from your score pile. If you do, draw a card of value two higher
        // than the highest card remaining in your score pile."
        Register(r, cards, "Education", Icon.Lightbulb, isDemand: false,
            text: "You may return the highest card from your score pile. If you do, draw a card of value two higher than the highest card remaining in your score pile.",
            handler: new EducationHandler());

        // Medicine (Yellow, Leaf, DEMAND): "Exchange the highest card in
        // your score pile with the lowest card in my score pile."
        Register(r, cards, "Medicine", Icon.Leaf, isDemand: true,
            text: "I demand you exchange the highest card in your score pile with the lowest card in my score pile!",
            handler: new MedicineDemandHandler());

        // Machinery (Yellow, Leaf): TWO effects —
        //   1. DEMAND: "Exchange all cards in your hand with all the highest
        //      cards in my hand."
        //   2. NON-DEMAND: "Score a card from your hand with a [Castle]. You
        //      may splay your red cards left."
        RegisterMulti(r, cards, "Machinery", Icon.Leaf,
            new DogmaEffect(true,
                "I demand you exchange all cards in your hand with all the highest cards in my hand!",
                new MachineryDemandHandler()),
            new DogmaEffect(false,
                "Score a card from your hand with a [Castle]. You may splay your red cards left.",
                new MachineryScoreCastleSplayHandler()));

        // Compass (Green, Crown, DEMAND): "Transfer a top non-green card
        // with a [Leaf] from your board to my board, and then transfer a
        // top card without a [Leaf] from my board to your board."
        Register(r, cards, "Compass", Icon.Crown, isDemand: true,
            text: "I demand you transfer a top non-green card with a [Leaf] from your board to my board, and then transfer a top card without a [Leaf] from my board to your board!",
            handler: new CompassDemandHandler());

        // Optics (Red, Crown): "Draw and meld a 3. If it has a [Crown],
        // draw and score a 4. Otherwise, transfer a card from your score
        // pile to the score pile of an opponent with fewer points than you."
        Register(r, cards, "Optics", Icon.Crown, isDemand: false,
            text: "Draw and meld a 3. If it has a [Crown], draw and score a 4. Otherwise, transfer a card from your score pile to the score pile of an opponent with fewer points than you.",
            handler: new OpticsHandler());

        // Alchemy (Blue, Castle): TWO effects —
        //   1. "Draw and reveal a 4 for every three [Castle] icons on your
        //      board. If any of the drawn cards are red, return the cards
        //      drawn and all cards in your hand. Otherwise, keep them."
        //   2. "Meld a card from your hand, then score a card from your hand."
        RegisterMulti(r, cards, "Alchemy", Icon.Castle,
            new DogmaEffect(false,
                "Draw and reveal a 4 for every three [Castle] icons on your board. If any of the drawn cards are red, return the cards drawn and all cards in your hand. Otherwise, keep them.",
                new AlchemyDrawRevealHandler()),
            new DogmaEffect(false,
                "Meld a card from your hand, then score a card from your hand.",
                new AlchemyMeldScoreHandler()));

        // Translation (Blue, Crown): TWO effects —
        //   1. "You may meld all the cards in your score pile. If you meld
        //      one, you must meld them all."
        //   2. "If each top card on your board has a [Crown], claim the
        //      World achievement."
        RegisterMulti(r, cards, "Translation", Icon.Crown,
            new DogmaEffect(false,
                "You may meld all the cards in your score pile. If you meld one, you must meld them all.",
                new TranslationMeldScoreHandler()),
            new DogmaEffect(false,
                "If each top card on your board has a [Crown], claim the World achievement.",
                new TranslationWorldHandler()));

        // --- Age 4 ---

        // Anatomy (Yellow, Leaf, DEMAND): "Return a card from your score
        // pile. If you do, return a top card of equal value from your board."
        Register(r, cards, "Anatomy", Icon.Leaf, isDemand: true,
            text: "I demand you return a card from your score pile! If you do, return a top card of equal value from your board!",
            handler: new AnatomyDemandHandler());

        // Colonialism (Red, Factory): "Draw and tuck a 3. If it has a
        // [Crown], repeat this dogma effect."
        Register(r, cards, "Colonialism", Icon.Factory, isDemand: false,
            text: "Draw and tuck a 3. If it has a [Crown], repeat this dogma effect.",
            handler: new ColonialismHandler());

        // Enterprise (Purple, Crown): TWO effects —
        //   1. DEMAND: transfer top non-purple Crown card; if so, activator
        //      draws and melds a 4.
        //   2. NON-DEMAND: may splay green right.
        RegisterMulti(r, cards, "Enterprise", Icon.Crown,
            new DogmaEffect(true,
                "I demand you transfer a top non-purple card with a [Crown] from your board to my board! If you do, draw and meld a 4!",
                new EnterpriseDemandHandler()),
            new DogmaEffect(false,
                "You may splay your green cards right.",
                new EnterpriseSplayHandler()));

        // Experimentation (Blue, Lightbulb): "Draw and meld a 5."
        Register(r, cards, "Experimentation", Icon.Lightbulb, isDemand: false,
            text: "Draw and meld a 5.",
            handler: new DrawAndMeldHandler(count: 1, startingAge: 5));

        // Gunpowder (Red, Factory): TWO effects —
        //   1. DEMAND: transfer top Castle card from board to activator score pile.
        //   2. NON-DEMAND: "If any card was transferred due to the demand,
        //      draw and score a 2."
        RegisterMulti(r, cards, "Gunpowder", Icon.Factory,
            new DogmaEffect(true,
                "I demand you transfer a top card with a [Castle] from your board to my score pile!",
                new GunpowderDemandHandler()),
            new DogmaEffect(false,
                "If any card was transferred due to the demand, draw and score a 2.",
                new GunpowderDrawIfDemandHandler()));

        // Invention (Green, Lightbulb): TWO effects —
        //   1. "Splay right a left-splayed color; if you do, draw and score a 4."
        //   2. "If five colors splayed (any direction), claim Wonder."
        RegisterMulti(r, cards, "Invention", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may splay right any one color of your cards currently splayed left. If you do, draw and score a 4.",
                new InventionSplayAndDrawHandler()),
            new DogmaEffect(false,
                "If you have five colors splayed, each in any direction, claim the Wonder achievement.",
                new InventionWonderHandler()));

        // Navigation (Green, Crown, DEMAND): "Transfer a 2 or 3 from your
        // score pile to my score pile."
        Register(r, cards, "Navigation", Icon.Crown, isDemand: true,
            text: "I demand you transfer a 2 or 3 from your score pile to my score pile!",
            handler: new NavigationDemandHandler());

        // Perspective (Yellow, Lightbulb): "Return a card from your hand.
        // If you do, score a card from your hand for every two [Lightbulb]
        // icons on your board."
        Register(r, cards, "Perspective", Icon.Lightbulb, isDemand: false,
            text: "You may return a card from your hand. If you do, score a card from your hand for every two [Lightbulb] icons on your board.",
            handler: new PerspectiveHandler());

        // Printing Press (Blue, Lightbulb): TWO effects —
        //   1. "Return a score-pile card; if you do, draw a card of value
        //      two higher than your top purple card."
        //   2. "You may splay your blue cards right."
        RegisterMulti(r, cards, "Printing Press", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may return a card from your score pile. If you do, draw a card of value two higher than the top purple card on your board.",
                new PrintingPressReturnAndDrawHandler()),
            new DogmaEffect(false,
                "You may splay your blue cards right.",
                new PrintingPressSplayHandler()));

        // Reformation (Purple, Leaf): TWO effects —
        //   1. "Tuck a hand card for every two [Leaf] icons on your board."
        //   2. "You may splay your yellow or purple cards right."
        RegisterMulti(r, cards, "Reformation", Icon.Leaf,
            new DogmaEffect(false,
                "You may tuck a card from your hand for every two [Leaf] icons on your board.",
                new ReformationTuckHandler()),
            new DogmaEffect(false,
                "You may splay your yellow or purple cards right.",
                new ReformationSplayHandler()));

        // ---------- Age 5 ----------

        // Astronomy (Purple, Lightbulb): TWO effects —
        //   1. Draw-and-reveal 6s, melding green/blue loop.
        //   2. If every non-purple top is age >=6 and you have >=3 achievements,
        //      claim Universe.
        RegisterMulti(r, cards, "Astronomy", Icon.Lightbulb,
            new DogmaEffect(false,
                "Draw and reveal a 6. If it is green or blue, meld it and repeat. "
              + "Otherwise, place it in your hand.",
                new AstronomyDrawMeldHandler()),
            new DogmaEffect(false,
                "If every non-purple top card is age 6 or higher, claim the Universe achievement.",
                new AstronomyUniverseHandler()));

        // Banking (Green, Crown): TWO effects —
        RegisterMulti(r, cards, "Banking", Icon.Crown,
            new DogmaEffect(true,
                "I demand you transfer a top non-green [Factory] card from your "
              + "board to my board! If you do, draw and score a 5!",
                new BankingDemandHandler()),
            new DogmaEffect(false,
                "You may splay your green cards right.",
                new BankingSplayHandler()));

        // Chemistry (Blue, Factory): TWO effects —
        RegisterMulti(r, cards, "Chemistry", Icon.Factory,
            new DogmaEffect(false,
                "You may splay your blue cards right.",
                new ChemistrySplayHandler()),
            new DogmaEffect(false,
                "Draw and score a card of value one higher than the highest top "
              + "card on your board, then return a card from your score pile.",
                new ChemistryDrawScoreReturnHandler()));

        // Coal (Red, Factory): THREE effects —
        RegisterMulti(r, cards, "Coal", Icon.Factory,
            new DogmaEffect(false, "Draw and tuck a 5.", new CoalTuckHandler()),
            new DogmaEffect(false, "You may splay your red cards right.", new CoalSplayHandler()),
            new DogmaEffect(false,
                "You may score any one top card on your board. If you do, also score the card beneath it.",
                new CoalScorePairHandler()));

        // Measurement (Green, Lightbulb): "Return a card from your hand.
        // If you do, splay right any one color of your choice, then draw
        // a card of value equal to the number of cards in that color."
        Register(r, cards, "Measurement", Icon.Lightbulb, isDemand: false,
            text: "Return a card from your hand. If you do, splay right any one color of "
                + "your choice, then draw a card of value equal to the number of cards in that color.",
            handler: new MeasurementHandler());

        // Physics (Blue, Lightbulb)
        Register(r, cards, "Physics", Icon.Lightbulb, isDemand: false,
            text: "Draw three 6 and reveal them. If two or more are the same color, "
                + "return the drawn cards and all cards in your hand.",
            handler: new PhysicsHandler());

        // The Pirate Code (Red, Crown): TWO effects —
        RegisterMulti(r, cards, "The Pirate Code", Icon.Crown,
            new DogmaEffect(true,
                "I demand you transfer two cards of value 4 or less from your score pile to my score pile!",
                new PirateCodeDemandHandler()),
            new DogmaEffect(false,
                "If any card was transferred due to the demand, score the lowest top card with a [Crown] from your board.",
                new PirateCodeScoreIfDemandHandler()));

        // Societies (Purple, Crown)
        Register(r, cards, "Societies", Icon.Crown, isDemand: true,
            text: "I demand you transfer a top non-purple card with a [Lightbulb] from your board to my board! "
                + "If you do, draw a 5!",
            handler: new SocietiesDemandHandler());

        // Statistics (Yellow, Leaf): TWO effects —
        RegisterMulti(r, cards, "Statistics", Icon.Leaf,
            new DogmaEffect(true,
                "I demand you draw the highest card in your score pile! If you do, "
              + "and have only one card in your hand afterwards, repeat this demand!",
                new StatisticsDemandHandler()),
            new DogmaEffect(false,
                "You may splay your yellow cards right.",
                new StatisticsSplayHandler()));

        // Steam Engine (Yellow, Factory)
        Register(r, cards, "Steam Engine", Icon.Factory, isDemand: false,
            text: "Draw and tuck two 4, then score your bottom yellow card.",
            handler: new SteamEngineHandler());

        // ---------- Age 6 ----------

        RegisterMulti(r, cards, "Atomic Theory", Icon.Lightbulb,
            new DogmaEffect(false, "You may splay your blue cards right.",
                new AtomicTheorySplayHandler()),
            new DogmaEffect(false, "Draw and meld a 7.",
                new AtomicTheoryDrawMeldHandler()));

        RegisterMulti(r, cards, "Canning", Icon.Factory,
            new DogmaEffect(false,
                "You may draw and tuck a 6. If you do, score all your top cards without a [Factory].",
                new CanningTuckIfHandler()),
            new DogmaEffect(false, "You may splay your yellow cards right.",
                new CanningSplayHandler()));

        Register(r, cards, "Classification", Icon.Lightbulb, isDemand: false,
            text: "Reveal the color of a card from your hand. Take into your hand all cards of "
                + "that color from all other players' hands. Then, meld all cards of that color from your hand.",
            handler: new ClassificationHandler());

        Register(r, cards, "Democracy", Icon.Lightbulb, isDemand: false,
            text: "You may return any number of cards from your hand. If you returned more cards "
                + "than any other player due to Democracy this dogma action, draw and score an 8.",
            handler: new DemocracyReturnHandler());

        RegisterMulti(r, cards, "Emancipation", Icon.Factory,
            new DogmaEffect(true,
                "I demand you transfer a card from your hand to my score pile! If you do, draw a 6!",
                new EmancipationDemandHandler()),
            new DogmaEffect(false, "You may splay your red or purple cards right.",
                new EmancipationSplayHandler()));

        Register(r, cards, "Encyclopedia", Icon.Crown, isDemand: false,
            text: "You may meld all the highest cards in your score pile.",
            handler: new EncyclopediaHandler());

        RegisterMulti(r, cards, "Industrialization", Icon.Factory,
            new DogmaEffect(false,
                "Draw and tuck a 6 for every two [Factory] icons on your board.",
                new IndustrializationTuckHandler()),
            new DogmaEffect(false, "You may splay your red or purple cards right.",
                new IndustrializationSplayHandler()));

        Register(r, cards, "Machine Tools", Icon.Factory, isDemand: false,
            text: "Draw and score a card of value equal to the highest card in your score pile.",
            handler: new MachineToolsHandler());

        RegisterMulti(r, cards, "Metric System", Icon.Crown,
            new DogmaEffect(false,
                "If your green cards are splayed right, you may splay any color of your cards right.",
                new MetricSystemAnyColorHandler()),
            new DogmaEffect(false, "You may splay your green cards right.",
                new MetricSystemSplayGreenHandler()));

        RegisterMulti(r, cards, "Vaccination", Icon.Leaf,
            new DogmaEffect(true,
                "I demand you return all the lowest cards in your score pile! "
              + "If you returned any, draw and meld a 6!",
                new VaccinationDemandHandler()),
            new DogmaEffect(false,
                "If any card was returned as a result of the demand, draw and meld a 7.",
                new VaccinationDrawIfDemandHandler()));

        // --- Age 7 ---

        Register(r, cards, "Bicycle", Icon.Crown, isDemand: false,
            text: "You may exchange all the cards in your hand with all the cards in your score pile.",
            handler: new BicycleHandler());

        Register(r, cards, "Combustion", Icon.Crown, isDemand: true,
            text: "I demand you transfer two cards from your score pile to my score pile!",
            handler: new CombustionDemandHandler());

        Register(r, cards, "Electricity", Icon.Factory, isDemand: false,
            text: "Return all your top cards without a Factory, then draw an 8 for each card you returned.",
            handler: new ElectricityHandler());

        Register(r, cards, "Evolution", Icon.Lightbulb, isDemand: false,
            text: "You may choose to either draw and score an 8 and then return a card from your score pile, "
                + "or draw a card of value one higher than the highest card in your score pile.",
            handler: new EvolutionHandler());

        Register(r, cards, "Explosives", Icon.Factory, isDemand: true,
            text: "I demand you transfer your three highest cards from your hand to my hand! "
                + "If you do, and then have no cards in hand, draw a 7!",
            handler: new ExplosivesDemandHandler());

        Register(r, cards, "Lighting", Icon.Leaf, isDemand: false,
            text: "You may tuck up to three cards from your hand. If you do, draw and score a 7 for "
                + "every different value of card you tucked.",
            handler: new LightingHandler());

        RegisterMulti(r, cards, "Publications", Icon.Lightbulb,
            new DogmaEffect(false, "You may rearrange the order of one color of cards on your board.",
                new PublicationsRearrangeHandler()),
            new DogmaEffect(false, "You may splay your yellow or blue cards up.",
                new PublicationsSplayHandler()));

        RegisterMulti(r, cards, "Railroad", Icon.Clock,
            new DogmaEffect(false, "Return all cards from your hand, then draw three 6s.",
                new RailroadReturnAndDrawHandler()),
            new DogmaEffect(false, "You may splay up any one color of your cards currently splayed right.",
                new RailroadSplayUpHandler()));

        RegisterMulti(r, cards, "Refrigeration", Icon.Leaf,
            new DogmaEffect(true, "I demand you return half (rounded down) of the cards in your hand!",
                new RefrigerationDemandHandler()),
            new DogmaEffect(false, "You may score a card from your hand.",
                new RefrigerationScoreHandler()));

        Register(r, cards, "Sanitation", Icon.Leaf, isDemand: true,
            text: "I demand you exchange the two highest cards in your hand with the lowest card in my hand!",
            handler: new SanitationDemandHandler());

        // --- Age 8 ---

        Register(r, cards, "Antibiotics", Icon.Leaf, isDemand: false,
            text: "You may return up to three cards from your hand. For every different value of card that you returned, draw two 8s.",
            handler: new AntibioticsHandler());

        RegisterMulti(r, cards, "Corporations", Icon.Factory,
            new DogmaEffect(true,
                "I demand you transfer a top non-green card with a [Factory] from your board to my score pile! If you do, draw and meld an 8.",
                new CorporationsDemandHandler()),
            new DogmaEffect(false, "Draw and meld an 8.",
                new DrawAndMeldHandler(count: 1, startingAge: 8)));

        RegisterMulti(r, cards, "Empiricism", Icon.Lightbulb,
            new DogmaEffect(false,
                "Choose two colors, then draw and reveal a 9. If it is either of the colors you chose, meld it and you may splay your cards of that color up.",
                new EmpiricismChooseAndDrawHandler()),
            new DogmaEffect(false,
                "If you have twenty or more [Lightbulb] icons on your board, you win.",
                new EmpiricismWinHandler()));

        RegisterMulti(r, cards, "Flight", Icon.Crown,
            new DogmaEffect(false,
                "If your red cards are splayed up, you may splay any one color of your cards up.",
                new FlightAnyColorHandler()),
            new DogmaEffect(false, "You may splay your red cards up.",
                new FlightSplayRedHandler()));

        RegisterMulti(r, cards, "Mass Media", Icon.Lightbulb,
            new DogmaEffect(false,
                "You may return a card from your hand. If you do, choose a value, and return all cards of that value from all score piles.",
                new MassMediaReturnAndPurgeHandler()),
            new DogmaEffect(false, "You may splay your purple cards up.",
                new MassMediaSplayHandler()));

        Register(r, cards, "Mobility", Icon.Factory, isDemand: true,
            text: "I demand you transfer your two highest non-red cards without a [Factory] from your board to my score pile! If you transferred any cards, draw an 8!",
            handler: new MobilityDemandHandler());

        Register(r, cards, "Quantum Theory", Icon.Clock, isDemand: false,
            text: "You may return up to two cards from your hand. If you return two, draw a 10 and then draw and score a 10.",
            handler: new QuantumTheoryHandler());

        Register(r, cards, "Rocketry", Icon.Clock, isDemand: false,
            text: "Return a card in any other player's score pile for every two [Clock] icons on your board.",
            handler: new RocketryHandler());

        Register(r, cards, "Skyscrapers", Icon.Crown, isDemand: true,
            text: "I demand you transfer a top non-yellow card with a [Clock] from your board to my board! If you do, score the card beneath it, and return all other cards from that pile!",
            handler: new SkyscrapersDemandHandler());

        Register(r, cards, "Socialism", Icon.Leaf, isDemand: false,
            text: "You may tuck all cards from your hand. If you tuck one, you must tuck them all. If you tucked at least one purple card, take all the lowest cards in each other player's hand into your hand.",
            handler: new SocialismHandler());

        // --- Age 9 ---

        RegisterMulti(r, cards, "Collaboration", Icon.Crown,
            new DogmaEffect(true,
                "I demand you draw two 9s and reveal them. Transfer the card of my choice to my board, and meld the other!",
                new CollaborationDemandHandler()),
            new DogmaEffect(false,
                "If you have ten or more green cards on your board, you win.",
                new CollaborationWinHandler()));

        Register(r, cards, "Composites", Icon.Factory, isDemand: true,
            text: "I demand you transfer all but one card from your hand to my hand! Also, transfer the highest card from your score pile to my score pile!",
            handler: new CompositesDemandHandler());

        RegisterMulti(r, cards, "Computers", Icon.Clock,
            new DogmaEffect(false, "You may splay your red cards or your green cards up.",
                new ComputersSplayHandler()),
            new DogmaEffect(false,
                "Draw and meld a 10, then execute its non-demand dogma effects for yourself only.",
                new ComputersDrawMeldExecuteHandler()));

        Register(r, cards, "Ecology", Icon.Lightbulb, isDemand: false,
            text: "You may return a card from your hand. If you do, score a card from your hand and draw two 10s.",
            handler: new EcologyHandler());

        RegisterMulti(r, cards, "Fission", Icon.Clock,
            new DogmaEffect(true,
                "I demand you draw a 10! If it is red, remove all hands, boards, and score piles from the game! If this occurs, the dogma action is complete.",
                new FissionDemandHandler()),
            new DogmaEffect(false,
                "Return a top card other than Fission from any player's board.",
                new FissionReturnHandler()));

        Register(r, cards, "Genetics", Icon.Lightbulb, isDemand: false,
            text: "Draw and meld a 10. Score all cards beneath it.",
            handler: new GeneticsHandler());

        RegisterMulti(r, cards, "Satellites", Icon.Clock,
            new DogmaEffect(false, "Return all cards from your hand, and draw three 8s.",
                new SatellitesReturnDrawHandler()),
            new DogmaEffect(false, "You may splay your purple cards up.",
                new SatellitesSplayHandler()),
            new DogmaEffect(false,
                "Meld a card from your hand and then execute each of its non-demand dogma effects for yourself only.",
                new SatellitesMeldExecuteHandler()));

        Register(r, cards, "Services", Icon.Leaf, isDemand: true,
            text: "I demand you transfer all the highest cards from your score pile to my hand! If you transferred any cards, then transfer a top card from my board without a [Leaf] to your hand!",
            handler: new ServicesDemandHandler());

        RegisterMulti(r, cards, "Specialization", Icon.Factory,
            new DogmaEffect(false,
                "Reveal a card from your hand. Take into your hand the top card of that color from all other players' boards.",
                new SpecializationRevealHandler()),
            new DogmaEffect(false, "You may splay your yellow or blue cards up.",
                new SpecializationSplayHandler()));

        Register(r, cards, "Suburbia", Icon.Leaf, isDemand: false,
            text: "You may tuck any number of cards from your hand. Draw and score a 1 for each card you tucked.",
            handler: new SuburbiaHandler());

        // --- Age 10 ---

        RegisterMulti(r, cards, "A.I.", Icon.Lightbulb,
            new DogmaEffect(false, "Draw and score a 10.",
                new DrawAndScoreHandler(count: 1, startingAge: 10)),
            new DogmaEffect(false,
                "If Robotics and Software are top cards on any board, the single player with the lowest score wins.",
                new AIWinHandler()));

        RegisterMulti(r, cards, "Bioengineering", Icon.Clock,
            new DogmaEffect(false,
                "Transfer a top card with a [Leaf] from any other player's board to your score pile.",
                new BioengineeringTransferHandler()),
            new DogmaEffect(false,
                "If any player has fewer than three [Leaf] icons on their board, the single player with the most [Leaf] icons on their board wins.",
                new BioengineeringWinHandler()));

        Register(r, cards, "Databases", Icon.Clock, isDemand: true,
            text: "I demand you return half (rounded up) of the cards in your score pile!",
            handler: new DatabasesDemandHandler());

        RegisterMulti(r, cards, "Globalization", Icon.Factory,
            new DogmaEffect(true,
                "I demand you return a top card with a [Leaf] on your board!",
                new GlobalizationDemandHandler()),
            new DogmaEffect(false,
                "Draw and score a 6. If no player has more [Leaf] icons than [Factory] icons on their board, the single player with the most points wins.",
                new GlobalizationDrawHandler()));

        Register(r, cards, "Miniaturization", Icon.Lightbulb, isDemand: false,
            text: "You may return a card from your hand. If you returned a 10, draw a 10 for every different value of card in your score pile.",
            handler: new MiniaturizationHandler());

        Register(r, cards, "Robotics", Icon.Factory, isDemand: false,
            text: "Score your top green card. Draw and meld a 10, then execute its non-demand dogma effects for yourself only.",
            handler: new RoboticsHandler());

        RegisterMulti(r, cards, "Self Service", Icon.Crown,
            new DogmaEffect(false,
                "Execute the non-demand dogma effects of any other top card on your board for yourself only.",
                new SelfServiceExecuteHandler()),
            new DogmaEffect(false,
                "If you have more achievements than each other player, you win.",
                new SelfServiceWinHandler()));

        RegisterMulti(r, cards, "Software", Icon.Clock,
            new DogmaEffect(false, "Draw and score a 10.",
                new DrawAndScoreHandler(count: 1, startingAge: 10)),
            new DogmaEffect(false,
                "Draw and meld two 10s, then execute the second card's non-demand dogma effects for yourself only.",
                new SoftwareMeldTwoExecuteHandler()));

        Register(r, cards, "Stem Cells", Icon.Leaf, isDemand: false,
            text: "You may score all cards from your hand. If you score one, you must score them all.",
            handler: new StemCellsHandler());

        RegisterMulti(r, cards, "The Internet", Icon.Clock,
            new DogmaEffect(false, "You may splay your green cards up.",
                new TheInternetSplayHandler()),
            new DogmaEffect(false, "Draw and score a 10.",
                new DrawAndScoreHandler(count: 1, startingAge: 10)),
            new DogmaEffect(false,
                "Draw and meld a 10 for every two [Clock] icons on your board.",
                new TheInternetDrawMeldPerClockHandler()));
    }

    private static void Register(
        CardRegistry r,
        IReadOnlyList<Card> cards,
        string title,
        Icon featured,
        bool isDemand,
        string text,
        IDogmaHandler handler)
    {
        var card = cards.Single(c => c.Title == title);
        var def = new DogmaDefinition(
            featured,
            new[] { new DogmaEffect(isDemand, text, handler) });
        r.Register(card.Id, def);
    }

    private static void RegisterMulti(
        CardRegistry r,
        IReadOnlyList<Card> cards,
        string title,
        Icon featured,
        params DogmaEffect[] effects)
    {
        var card = cards.Single(c => c.Title == title);
        r.Register(card.Id, new DogmaDefinition(featured, effects));
    }
}
