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
    public class GreedyHandler : IAnimalPersonalityHandler
    {
        private readonly TileService Tiles;
        public GreedyHandler(TileService tiles) => Tiles = tiles;

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();
            a.speed = 2;

            bool crowded = ctx.Farm.getAllFarmAnimals()
                .Any(other => other != a && Vector2.Distance(other.TilePoint.ToVector2(), a.TilePoint.ToVector2()) < 3f);

            if (crowded)
                list.Add(() => TryAvoidCrowd(a, ctx));

            return list;
        }

        private bool TryAvoidCrowd(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.5) return false;
            Vector2 moveTile = StardewValley.Utility.getRandomAdjacentOpenTile(a.TilePoint.ToVector2(), ctx.Farm);
            if (moveTile == Vector2.Zero) return false;

            a.controller = new PathFindController(a, a.currentLocation, moveTile.ToPoint(), 2);
            return true;
        }
    }
}
