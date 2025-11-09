using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public class LazyHandler : IAnimalPersonalityHandler
    {
        private readonly Dictionary<long, int> _cooldown = new();
        private static bool CooldownOK(Dictionary<long, int> cd, long id, int minTicks)
        {
            int now = Game1.ticks;
            if (cd.TryGetValue(id, out var last) && now - last < minTicks) return false;
            cd[id] = now; return true;
        }

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();
            a.speed = 1;

            // 1) Nap is always feasible
            list.Add(() => TryNap(a, ctx));

            // 2) Prefer staying close to the barn door
            if (a.home != null && a.currentLocation?.IsOutdoors == true)
            {
                var door = a.home.animalDoor.Value;
                var doorTile = new Vector2(door.X, door.Y);
                float dist = Vector2.Distance(a.TilePoint.ToVector2(), doorTile);

                // if door is open and we're far away -> amble back
                if (a.home.animalDoorOpen.Value && dist > 6f)
                    list.Add(() => TryReturnToDoor(a, door, ctx));
                else // door closed or already nearby -> short lazy wander near home
                    list.Add(() => TryWanderNearHome(a, ctx));
            }

            return list;
        }

        private bool TryNap(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 900)) return false; // ~15s
            // higher chance later in the day
            double baseChance = Game1.timeOfDay >= 1700 ? 0.12 : 0.06;
            if (ctx.Rng.NextDouble() >= baseChance) return false;

            a.controller = null;
            if (a.currentLocation == Game1.currentLocation && Utility.isOnScreen(a.Position, 128))
                a.doEmote(24); // Zzz
            return true;
        }

        private bool TryReturnToDoor(FarmAnimal a, Point door, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.6) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, door, 2);
            }
            return true;
        }

        private bool TryWanderNearHome(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.35) return false;

            var pos = a.TilePoint.ToVector2();
            var nearby = pos + new Vector2(ctx.Rng.Next(-2, 3), ctx.Rng.Next(-2, 3));

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, nearby.ToPoint(), 2);
            }
            return true;
        }
    }
}
