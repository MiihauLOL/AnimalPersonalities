using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public class EnergeticHandler : IAnimalPersonalityHandler
    {
        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();
            a.speed = 3;
            bool timeOk = StardewValley.Game1.timeOfDay < 2300;

            if (!timeOk) return list;

            Vector2 pos = a.TilePoint.ToVector2();
            Vector2 burstTarget = pos + new Vector2(ctx.Rng.Next(-4, 5), ctx.Rng.Next(-4, 5));
            list.Add(() => TryBurst(a, burstTarget, ctx));

            Vector2 exploreTarget = StardewValley.Utility.getRandomAdjacentOpenTile(pos, ctx.Farm);
            if (exploreTarget != Vector2.Zero)
                list.Add(() => TryExplore(a, exploreTarget, ctx));

            return list;
        }

        private bool TryBurst(FarmAnimal a, Vector2 target, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.7) return false;
            a.controller = new PathFindController(a, a.currentLocation, target.ToPoint(), 2);
            a.doEmote(20);
            return true;
        }

        private bool TryExplore(FarmAnimal a, Vector2 target, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.7) return false;
            a.controller = new PathFindController(a, a.currentLocation, target.ToPoint(), 2);
            return true;
        }
    }
}
