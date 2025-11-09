using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public class EnergeticHandler : IAnimalPersonalityHandler
    {
        // tiny anti-spam so they don't burst every tick
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
            if (Game1.timeOfDay >= 2330) return actions; // sleep a bit later, but not too late

            // explore (outdoors, can pick a reachable tile)
            if (a.currentLocation?.IsOutdoors == true)
            {
                actions.Add(() => TryExplore(a, ctx));
                actions.Add(() => TryBurst(a, ctx)); // run fast OR explore — one action will be chosen by the caller
            }

            return actions;
        }

        private bool TryBurst(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.65) return false;

            a.speed = Math.Max(a.speed, 4);
            var here = a.TilePoint.ToVector2();
            var target = here + new Vector2(ctx.Rng.Next(-5, 6), ctx.Rng.Next(-5, 6));

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, target.ToPoint(), 2);
            }

            // happy emote on burst (on screen only)
            if (a.currentLocation == Game1.currentLocation && Utility.isOnScreen(a.Position, 128))
                a.doEmote(20);

            DelayedAction.functionAfterDelay(() =>
            {
                if (a != null) a.speed = 2;
            }, 1200);
            return true;
        }

        private bool TryExplore(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.65) return false;

            var here = a.TilePoint.ToVector2();
            var target = here + new Vector2(ctx.Rng.Next(-8, 9), ctx.Rng.Next(-8, 9));

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, target.ToPoint(), 2);
            }
            return true;
        }
    }
}
