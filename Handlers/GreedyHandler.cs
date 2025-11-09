using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalPersonalities.Handlers
{
    public class GreedyHandler : IAnimalPersonalityHandler
    {
        private readonly TileService _tiles;
        public GreedyHandler(TileService tiles) => _tiles = tiles;

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
            a.speed = 2;

            // crowd avoidance is only feasible if anyone is close
            var me = a.TilePoint.ToVector2();
            bool crowded = ctx.Farm.getAllFarmAnimals()
                .Any(other => other != a
                              && other.currentLocation == a.currentLocation
                              && Vector2.Distance(other.TilePoint.ToVector2(), me) < 3f);

            if (crowded)
                list.Add(() => TryAvoidCrowd(a, ctx));

            // eating twice is handled in the morning service already.
            return list;
        }

        private bool TryAvoidCrowd(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 300)) return false; // ~5s
            if (ctx.Rng.NextDouble() >= 0.55) return false;

            // pick a nearby open tile away from crowd center
            var here = a.TilePoint.ToVector2();
            var others = a.currentLocation.animals.Values.Where(o => o != a).Select(o => o.TilePoint.ToVector2()).ToList();
            Vector2 away = here;
            if (others.Count > 0)
            {
                var center = new Vector2((float)others.Average(v => v.X), (float)others.Average(v => v.Y));
                var dir = here - center;
                if (dir.LengthSquared() < 0.1f) dir = new Vector2(ctx.Rng.Next(-1, 2), ctx.Rng.Next(-1, 2));
                dir.Normalize();
                away = here + dir * 3f;
            }

            var dest = new Vector2((int)Math.Round(away.X), (int)Math.Round(away.Y));
            if (!_tiles.IsClearTile(a.currentLocation, dest)) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, dest.ToPoint(), 2);
            }
            return true;
        }
    }
}
