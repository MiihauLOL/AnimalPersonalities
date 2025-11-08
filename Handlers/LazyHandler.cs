using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public class LazyHandler : IAnimalPersonalityHandler
    {
        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();
            var pos = a.TilePoint.ToVector2();
            a.speed = 1;

            // Nap (always feasible)
            list.Add(() => TryNap(a, ctx));

            // Door logic
            if (a.home != null)
            {
                bool doorOpen = a.home.animalDoorOpen.Value;
                var door = a.home.animalDoor.Value;
                var doorTile = new Vector2(door.X, door.Y);
                float dist = Vector2.Distance(pos, doorTile);

                if (doorOpen && dist > 6f)
                    list.Add(() => TryReturnToDoor(a, door, ctx));
                else if (!doorOpen)
                    list.Add(() => TryWanderNear(a, ctx));
            }

            return list;
        }

        private bool TryNap(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.05) return false;
            a.controller = null;
            a.doEmote(4);
            return true;
        }

        private bool TryReturnToDoor(FarmAnimal a, Point door, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.5) return false;
            a.controller = new PathFindController(a, a.currentLocation, door, 2);
            return true;
        }

        private bool TryWanderNear(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.2) return false;
            Vector2 nearby = a.TilePoint.ToVector2() + new Vector2(ctx.Rng.Next(-2, 3), ctx.Rng.Next(-2, 3));
            a.controller = new PathFindController(a, a.currentLocation, nearby.ToPoint(), 2);
            a.doEmote(24);
            return true;
        }
    }
}
