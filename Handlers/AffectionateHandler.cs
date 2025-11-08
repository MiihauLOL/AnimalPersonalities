using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalPersonalities.Handlers
{
    public class AffectionateHandler : IAnimalPersonalityHandler
    {
        private readonly EmoteCooldownService Emotes;
        public AffectionateHandler(EmoteCooldownService emotes) => Emotes = emotes;

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();

            var farmer = StardewValley.Game1.player;
            Vector2 aPos = a.TilePoint.ToVector2();
            Vector2 fPos = farmer.TilePoint.ToVector2();

            // farmer nearby?
            if (Vector2.Distance(aPos, fPos) < 8f)
                list.Add(() => TryRunToFarmer(a, ctx));

            // buddy nearby?
            var buddy = ctx.Farm.getAllFarmAnimals()
                .Where(other =>
                    other != a &&
                    other.modData.TryGetValue(ctx.PersonalityKey, out string p) && p == "Affectionate" &&
                    Vector2.Distance(other.TilePoint.ToVector2(), aPos) < 8f)
                .OrderBy(other => Vector2.Distance(other.TilePoint.ToVector2(), aPos))
                .FirstOrDefault();

            if (buddy != null)
                list.Add(() => TryBuddyHangout(a, buddy, ctx));

            // passive friendship always feasible
            list.Add(() => TryFriendshipTick(a, ctx));

            return list;
        }

        private bool TryRunToFarmer(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.25) return false;

            var farmer = StardewValley.Game1.player;
            a.speed = 3;
            a.controller = new PathFindController(a, farmer.currentLocation, farmer.TilePoint, 2);

            if (Emotes.CanDoEmote(a))
            {
                a.doEmote(12);
                StardewValley.Game1.playSound("smallSelect");
                Emotes.SetEmote(a);
            }

            DelayedAction.functionAfterDelay(() =>
            {
                if (a != null) a.speed = 2;
            }, 1500);

            return true;
        }

        private bool TryBuddyHangout(FarmAnimal a, FarmAnimal buddy, AIContext ctx)
        {
            double chance = 0.05;
            if (buddy.home?.buildingType.Value == a.home?.buildingType.Value) chance += 0.10;
            if (buddy.type.Value == a.type.Value) chance += 0.15;

            if (ctx.Rng.NextDouble() >= chance) return false;

            a.speed = 2;
            a.controller = new PathFindController(a, a.currentLocation, buddy.TilePoint, 2);

            DelayedAction.functionAfterDelay(() =>
            {
                if (Vector2.Distance(a.TilePoint.ToVector2(), buddy.TilePoint.ToVector2()) <= 2f)
                {
                    if (Emotes.CanDoEmote(a) && Emotes.CanDoEmote(buddy))
                    {
                        a.doEmote(12);
                        buddy.doEmote(12);
                        Emotes.SetEmote(a);
                        Emotes.SetEmote(buddy);
                    }
                }
            }, 1000);

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
