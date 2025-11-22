using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
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
            var acts = new List<Func<bool>>();
            if (a?.currentLocation == null) return acts;

            // greedy animals are a little brisk
            a.speed = 2;

            var loc = a.currentLocation;
            var me = a.TilePoint.ToVector2();

            // 1) Outdoors: aggressively hunt for good grass when not full.
            if (loc.IsOutdoors && a.fullness.Value < 255 && FindBestGrassTile(loc, me, 12, a, out var grassTile))
                acts.Add(() => TryHuntGrass(a, ctx, grassTile));

            // 2) Indoors morning: camp the animal door to get out first.
            if (!loc.IsOutdoors && a.home != null && a.home.animalDoorOpen.Value && Game1.timeOfDay is >= 600 and < 1000)
            {
                var doorAbs = new Point(a.home.tileX.Value + a.home.animalDoor.X,
                                        a.home.tileY.Value + a.home.animalDoor.Y);
                // find a clear adjacent tile to stand on
                if (TryGetAdjacentStandTile(loc, doorAbs, out var standTile))
                    acts.Add(() => TryCampDoor(a, ctx, standTile));
            }

            // 3) Keep distance from others (protect the snack zone).
            if (IsCrowdedNear(a, 3f))
                acts.Add(() => TryAvoidCrowd(a, ctx));

            return acts;
        }

        // ---------- actions ----------

        private bool TryHuntGrass(FarmAnimal a, AIContext ctx, Vector2 targetTile)
        {
            // check cooldown and roll odds after feasibility has been established
            if (!CooldownOK(_cooldown, a.myID.Value, 360)) return false; // ~6s
            if (ctx.Rng.NextDouble() >= 0.75) return false;

            var loc = a.currentLocation;

            // path toward the chosen grass tile
            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, loc, targetTile.ToPoint(), 2);
            }

            // once there (or close), try to eat shortly after
            StardewValley.DelayedAction.functionAfterDelay(() =>
            {
                if (a?.currentLocation != loc) return;

                // if we actually reached the tile, trigger eat
                if (a.TilePoint.ToVector2() == targetTile)
                    a.eatGrass(loc); // vanilla will set fullness and handle grass removal
            }, 900);

            return true;
        }

        private bool TryCampDoor(FarmAnimal a, AIContext ctx, Vector2 standTile)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.7) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, standTile.ToPoint(), 2);
            }
            return true;
        }

        private bool TryAvoidCrowd(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 300)) return false; // ~5s
            if (ctx.Rng.NextDouble() >= 0.55) return false;

            var here = a.TilePoint.ToVector2();
            var others = a.currentLocation.animals.Values.Where(o => o != a)
                         .Select(o => o.TilePoint.ToVector2()).ToList();

            Vector2 away = here;
            if (others.Count > 0)
            {
                var center = new Vector2((float)others.Average(v => v.X),
                                         (float)others.Average(v => v.Y));
                var dir = here - center;
                if (dir.LengthSquared() < 0.1f)
                    dir = new Vector2(ctx.Rng.Next(-1, 2), ctx.Rng.Next(-1, 2));
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

        // ---------- helpers ----------

        private static bool IsCrowdedNear(FarmAnimal a, float maxDistTiles)
        {
            var me = a.TilePoint.ToVector2();
            return a.currentLocation.animals.Values.Any(o => o != a &&
                Vector2.DistanceSquared(o.TilePoint.ToVector2(), me) <= maxDistTiles * maxDistTiles);
        }

        // pick the “best” grass tile nearby: prefer type 7, de-prioritize crowded spots
        private static bool FindBestGrassTile(GameLocation loc, Vector2 origin, int radius,
                                              FarmAnimal me, out Vector2 bestTile)
        {
            bestTile = default;
            var candidates = new List<(Vector2 tile, float score)>();

            foreach (var pair in loc.terrainFeatures.Pairs)
            {
                if (pair.Value is not Grass g) continue;

                var t = pair.Key;
                float dist = Vector2.Distance(origin, t);
                if (dist > radius) continue;

                // base score: nearby wins
                float score = 10f - dist;

                // prefer “type 7” grass (yields more friendship in vanilla Eat())
                if (g.grassType.Value == 7) score += 3f;

                // avoid crowds at the destination, greed wants the patch to itself
                int nearbyAnimals = loc.animals.Values.Count(a =>
                    a != me && Vector2.Distance(a.TilePoint.ToVector2(), t) <= 2.0f);
                score -= nearbyAnimals * 1.5f;

                // avoid blocked arrival tiles
                if (loc.isTileOnMap(t) && loc.isTileLocationOpen(t))
                    candidates.Add((t, score));
            }

            if (candidates.Count == 0) return false;

            // choose the highest score (greedy!)
            bestTile = candidates.OrderByDescending(c => c.score).First().tile;
            return true;
        }

        private static bool TryGetAdjacentStandTile(GameLocation loc, Point center, out Vector2 standTile)
        {
            foreach (var off in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            {
                var t = new Vector2(center.X + off.X, center.Y + off.Y);
                if (loc.isTileOnMap(t) && loc.isTileLocationOpen(t))
                { standTile = t; return true; }
            }
            standTile = default;
            return false;
        }
    }
}
