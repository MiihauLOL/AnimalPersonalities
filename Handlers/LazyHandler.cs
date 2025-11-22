using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using SObject = StardewValley.Object;

namespace AnimalPersonalities.Handlers
{
    public class LazyHandler : IAnimalPersonalityHandler
    {
        private readonly Dictionary<long, int> _cooldown = new();
        private static bool CooldownOK(Dictionary<long, int> cd, long id, int minTicks)
        {
            int now = Game1.ticks;
            if (cd.TryGetValue(id, out int last) && now - last < minTicks) return false;
            cd[id] = now; return true;
        }

        private const float LeashRadiusTiles = 6f;

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var acts = new List<Func<bool>>();
            if (a?.currentLocation == null) return acts;

            a.speed = 1;

            // small nap (always considered)
            acts.Add(() => TryNap(a, ctx));

            // NEW: short lie-down if the animal has a SleepFrame and isn’t swimming
            if (SupportsLieDown(a))
                acts.Add(() => TryLieDown(a, ctx));

            var loc = a.currentLocation;
            bool outdoors = loc.IsOutdoors;
            var myTile = a.TilePoint.ToVector2();

            // absolute animal-door tile
            Point doorAbs = default;
            float distToDoor = float.MaxValue;
            bool hasHome = a.home != null;
            if (hasHome)
            {
                doorAbs = new Point(a.home.tileX.Value + a.home.animalDoor.X,
                                    a.home.tileY.Value + a.home.animalDoor.Y);
                distToDoor = Vector2.Distance(myTile, new Vector2(doorAbs.X, doorAbs.Y));
            }

            // comfy spots
            if (outdoors && FindShadeTile(loc, myTile, 8, out var shadeTile))
                acts.Add(() => TryWalkAndLounge(a, ctx, shadeTile));

            if (!outdoors && FindHeaterTile(loc, myTile, 10, out var heaterAdj))
                acts.Add(() => TryWalkAndLounge(a, ctx, heaterAdj));

            // prefer staying near home
            if (hasHome)
            {
                if (outdoors && a.home.animalDoorOpen.Value && distToDoor > LeashRadiusTiles)
                    acts.Add(() => TryReturnToDoor(a, doorAbs, ctx));

                if (outdoors && a.home.animalDoorOpen.Value && Game1.timeOfDay >= 1700)
                    acts.Add(() => TryReturnToDoor(a, doorAbs, ctx));
            }

            // tiny wander
            acts.Add(() => TryShortWander(a, ctx, outdoors ? 2 : 1));

            return acts;
        }

        // ---------- actions ----------

        private bool TryNap(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 720)) return false; // ~12s
            double baseChance = Game1.timeOfDay >= 1700 ? 0.14 : 0.07;
            if (ctx.Rng.NextDouble() >= baseChance) return false;

            a.controller = null;
            a.Halt();
            a.pauseTimer = ctx.Rng.Next(900, 1600); // ~1–1.6s
            if (a.currentLocation == Game1.currentLocation && Utility.isOnScreen(a.Position, 128))
                a.doEmote(24); // Zzz
            return true;
        }

        // NEW: brief lie-down using the animal’s SleepFrame
        private bool TryLieDown(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 1200)) return false; // ~20s
            // a bit rarer than a nap
            if (ctx.Rng.NextDouble() >= 0.10) return false;
            if (a.IsActuallySwimming()) return false; // looks weird in water

            int sleepFrame = a.GetAnimalData()?.SleepFrame ?? -1;
            if (sleepFrame < 0) return false;

            a.controller = null;
            a.Halt();
            a.uniqueFrameAccumulator = -1; // don’t cycle unique idles while we lounge
            a.FacingDirection = 2;         // face down like the vanilla sleep pose
            a.Sprite.currentFrame = sleepFrame;
            a.Sprite.UpdateSourceRect();

            // lounge a bit longer than a nap
            a.pauseTimer = ctx.Rng.Next(1400, 2400);

            if (a.currentLocation == Game1.currentLocation && Utility.isOnScreen(a.Position, 128))
                a.doEmote(24); // small Zzz

            return true;
        }

        private bool TryReturnToDoor(FarmAnimal a, Point doorAbs, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false; // ~7s
            if (ctx.Rng.NextDouble() >= 0.65) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, doorAbs, 2);
            }
            return true;
        }

        private bool TryWalkAndLounge(FarmAnimal a, AIContext ctx, Vector2 targetTile)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 480)) return false; // ~8s
            if (ctx.Rng.NextDouble() >= 0.5) return false;

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, targetTile.ToPoint(), 2);
            }

            StardewValley.DelayedAction.functionAfterDelay(() =>
            {
                if (a == null) return;
                a.pauseTimer = 900;
            }, 900);

            return true;
        }

        private bool TryShortWander(FarmAnimal a, AIContext ctx, int radiusTiles)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 420)) return false;
            if (ctx.Rng.NextDouble() >= 0.35) return false;

            var pos = a.TilePoint.ToVector2();
            var dest = pos + new Vector2(ctx.Rng.Next(-radiusTiles, radiusTiles + 1),
                                         ctx.Rng.Next(-radiusTiles, radiusTiles + 1));

            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new PathFindController(a, a.currentLocation, dest.ToPoint(), 2);
            }
            return true;
        }

        // ---------- helpers ----------

        private static bool SupportsLieDown(FarmAnimal a)
        {
            // if the data exposes a valid sleep frame, we can fake a brief lie-down
            return (a.GetAnimalData()?.SleepFrame ?? -1) >= 0 && !a.IsActuallySwimming();
        }

        private static bool IsClearTile(GameLocation loc, Vector2 tile)
        {
            return loc.isTileOnMap(tile) && loc.isTileLocationOpen(tile);
        }

        private static bool FindShadeTile(GameLocation loc, Vector2 origin, int radius, out Vector2 shadeTile)
        {
            shadeTile = default;
            var cover = loc.terrainFeatures.Pairs
                .Where(kv => kv.Value is Tree || kv.Value is FruitTree)
                .Select(kv => kv.Key)
                .Where(t => Vector2.Distance(t, origin) <= radius)
                .OrderBy(t => Vector2.Distance(t, origin))
                .ToList();

            foreach (var t in cover)
            {
                var candidates = new[]
                {
                    t + new Vector2( 1, 0),
                    t + new Vector2(-1, 0),
                    t + new Vector2( 0, 1),
                    t + new Vector2( 0,-1),
                };
                foreach (var c in candidates)
                {
                    if (IsClearTile(loc, c))
                    { shadeTile = c; return true; }
                }
            }
            return false;
        }

        private static bool FindHeaterTile(GameLocation loc, Vector2 origin, int radius, out Vector2 heaterAdj)
        {
            heaterAdj = default;

            if (!(Game1.IsWinter || Game1.timeOfDay >= 1700))
            {
                if (Game1.random.NextDouble() < 0.5) return false;
            }

            var heaters = loc.objects.Pairs
                .Where(kv => kv.Value is SObject o && o.bigCraftable.Value && (o.Name == "Heater" || o.DisplayName == "Heater"))
                .Select(kv => kv.Key)
                .Where(t => Vector2.Distance(t, origin) <= radius)
                .OrderBy(t => Vector2.Distance(t, origin))
                .ToList();

            foreach (var h in heaters)
            {
                var adj = new[]
                {
                    h + new Vector2( 1, 0),
                    h + new Vector2(-1, 0),
                    h + new Vector2( 0, 1),
                    h + new Vector2( 0,-1),
                };
                foreach (var c in adj)
                {
                    if (IsClearTile(loc, c))
                    { heaterAdj = c; return true; }
                }
            }
            return false;
        }
    }
}
