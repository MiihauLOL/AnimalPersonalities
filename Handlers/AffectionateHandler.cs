using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalPersonalities.Handlers
{
    public class AffectionateHandler : IAnimalPersonalityHandler
    {
        private readonly EmoteCooldownService _emotes;
        public AffectionateHandler(EmoteCooldownService emotes) => _emotes = emotes;

        // simple anti-spam: one action per ~10s per animal
        private readonly Dictionary<long, int> _cooldown = new();
        private static bool CooldownOK(Dictionary<long, int> cd, long id, int minTicks)
        {
            int now = Game1.ticks;
            if (cd.TryGetValue(id, out var last) && now - last < minTicks) return false;
            cd[id] = now; return true;
        }

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var actions = new List<Func<bool>>();
            var farmer = Game1.player;

            // 1) run to farmer (only if feasible)
            if (a.currentLocation == farmer.currentLocation)
            {
                var aTile = a.TilePoint.ToVector2();
                var fTile = farmer.TilePoint.ToVector2();
                if (Vector2.Distance(aTile, fTile) <= 8f)
                    actions.Add(() => TryRunToFarmer(a, ctx));
            }

            // 2) buddy hangout (only if an affectionate buddy is nearby)
            var meTile = a.TilePoint.ToVector2();
            var buddy = ctx.Farm.getAllFarmAnimals()
                .Where(o => o != a
                            && o.currentLocation == a.currentLocation
                            && o.modData.TryGetValue(ctx.PersonalityKey, out var p) && p == "Affectionate"
                            && Vector2.Distance(o.TilePoint.ToVector2(), meTile) <= 8f)
                .OrderBy(o => Vector2.Distance(o.TilePoint.ToVector2(), meTile))
                .FirstOrDefault();

            if (buddy != null)
                actions.Add(() => TryBuddyHangout(a, buddy, ctx));

            // 3) tiny passive friendship nudge (always feasible)
            actions.Add(() => TryFriendshipTick(a, ctx));

            return actions;
        }

        private bool TryRunToFarmer(FarmAnimal a, AIContext ctx)
        {
            // feasibility already checked; now roll odds
            if (!CooldownOK(_cooldown, a.myID.Value, 600)) return false; // ~10s
            if (ctx.Rng.NextDouble() >= 0.30) return false;              // 30%

            var farmer = Game1.player;
            if (a.currentLocation != farmer.currentLocation) return false;

            a.speed = Math.Max(a.speed, 3);
            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, farmer.TilePoint, 2);
            }

            if (_emotes.CanDoEmote(a))
            {
                a.doEmote(20); // heart
                _emotes.SetEmote(a);
            }

            // restore speed after a short dash
            DelayedAction.functionAfterDelay(() =>
            {
                if (a != null) a.speed = 2;
            }, 1500);

            return true;
        }

        private bool TryBuddyHangout(FarmAnimal a, FarmAnimal buddy, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 600)) return false; // ~10s

            // Base chance & boosts (same home/type)
            double chance = 0.08; // base
            if (buddy.home?.buildingType.Value == a.home?.buildingType.Value) chance += 0.10;
            if (buddy.type.Value == a.type.Value) chance += 0.12;

            if (ctx.Rng.NextDouble() >= chance) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.speed = 2;
                a.controller = new PathFindController(a, a.currentLocation, buddy.TilePoint, 2);
            }

            // small simultaneous heart when they meet
            DelayedAction.functionAfterDelay(() =>
            {
                if (Vector2.Distance(a.TilePoint.ToVector2(), buddy.TilePoint.ToVector2()) <= 2f)
                {
                    if (_emotes.CanDoEmote(a)) { a.doEmote(20); _emotes.SetEmote(a); }
                    if (_emotes.CanDoEmote(buddy)) { buddy.doEmote(20); _emotes.SetEmote(buddy); }
                }
            }, 900);

            return true;
        }

        private bool TryFriendshipTick(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() < 0.05)
            {
                a.friendshipTowardFarmer.Value += 1;
                return true;
            }
            return false;
        }
    }
}
