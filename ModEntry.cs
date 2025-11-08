using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Buildings;
using StardewValley.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using AnimalPersonalities.Services;
using AnimalPersonalities.Services.UI;
using AnimalPersonalities.Handlers;

namespace AnimalPersonalities
{
    public class ModEntry : Mod
    {
        // services
        private EmoteCooldownService Emotes;
        private TileService Tiles;
        private MorningGreedyService GreedyMorning;
        private PersonalityAssigner Assigner;
        private AnimalQueryUI QueryUI;
        private AnimalPageUI AnimalListUI;
        

        // handlers
        private readonly Dictionary<Personality, IAnimalPersonalityHandler> Handlers =
            new Dictionary<Personality, IAnimalPersonalityHandler>();

        private readonly Random Rng = new();

        // stored here so every class uses the same key
        internal string PersonalityKey => $"{ModManifest.UniqueID}/Personality";

        public override void Entry(IModHelper helper)
        {
            // core services
            Emotes = new EmoteCooldownService();
            Tiles = new TileService();
            GreedyMorning = new MorningGreedyService();
            Assigner = new PersonalityAssigner();
            QueryUI = new AnimalPersonalities.Services.UI.AnimalQueryUI(PersonalityKey, Helper);
            AnimalListUI = new AnimalPersonalities.Services.UI.AnimalPageUI(PersonalityKey, Helper);


            // handlers (inject services as needed)
            Handlers[Personality.Lazy] = new LazyHandler();
            Handlers[Personality.Energetic] = new EnergeticHandler();
            Handlers[Personality.Mischievous] = new MischievousHandler(Tiles);
            Handlers[Personality.Affectionate] = new AffectionateHandler(Emotes);
            Handlers[Personality.Greedy] = new GreedyHandler(Tiles);

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Helper.Events.Display.RenderedActiveMenu += QueryUI.OnRenderedMenu;
            Helper.Events.Display.RenderedActiveMenu += AnimalListUI.OnRenderedMenu;
            helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            // When you finish naming/purchasing an animal, the menu closes.
            bool finishedPurchaseOrNaming =
                (e.OldMenu is PurchaseAnimalsMenu || e.OldMenu is NamingMenu) &&
                (e.NewMenu is not PurchaseAnimalsMenu && e.NewMenu is not NamingMenu);

            if (!finishedPurchaseOrNaming) return;

            // Assign personality right after the game actually adds the animal.
            DelayedAction.functionAfterDelay(() =>
            {
                foreach (var b in Game1.getFarm().buildings)
                {
                    if (b.indoors.Value is not AnimalHouse house) continue;
                    foreach (var a in house.animals.Values)
                        Assigner.AssignIfMissing(a, Rng, PersonalityKey, Monitor, toast: true);
                }
            }, 1000);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // ensure all animals have a personality
            foreach (var b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value is not AnimalHouse house) continue;
                foreach (var a in house.animals.Values)
                    Assigner.AssignIfMissing(a, Rng, PersonalityKey, Monitor, toast: false);
            }

            // once-per-day greedy effect
            GreedyMorning.Apply(Game1.getFarm(), PersonalityKey, Rng, Monitor);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.IsMultipleOf(300)) // every 5 seconds
                return;

            var ctx = new AIContext
            {
                Rng = Rng,
                Monitor = Monitor,
                Helper = Helper,
                Farm = Game1.getFarm(),
                PersonalityKey = PersonalityKey,
                Emotes = Emotes,
                Tiles = Tiles
            };

            foreach (var b in ctx.Farm.buildings)
            {
                if (b.indoors.Value is not AnimalHouse house) continue;

                foreach (var animal in house.animals.Values)
                {
                    if (!animal.modData.TryGetValue(PersonalityKey, out string pStr)) continue;
                    if (!Enum.TryParse(pStr, out Personality p)) continue;

                    if (!Handlers.TryGetValue(p, out var handler)) continue;

                    // Build feasible actions first; if any, randomly pick ONE and try it.
                    var actions = handler.BuildFeasibleActions(animal, ctx);
                    if (actions.Count > 0)
                    {
                        var chosen = actions[ctx.Rng.Next(actions.Count)];
                        _ = chosen(); // each action returns bool to indicate it fired (not used now)
                    }
                }
            }
        }
    }
}
