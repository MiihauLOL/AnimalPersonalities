using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;


namespace AnimalPersonalities.Handlers
{
    // Mischievous: choose at most one feasible action per check.
    public class MischievousHandler : IAnimalPersonalityHandler
    {
        private readonly TileService _tiles;
        private readonly IMonitor _monitor;
        private readonly HopArcService _hopArc;

        // odds
        private const double HideChance = 0.20; 
        private const double GatePrankChance = 0.02;
        private const double DoorPrankChance = 0.01;
        private const double FenceHopChance = 0.01;

        public MischievousHandler(TileService tiles, IMonitor monitor, HopArcService hopArc)
        { _tiles = tiles; _monitor = monitor; _hopArc = hopArc; }

        // per-animal cooldowns
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
            var loc = a.currentLocation;
            if (loc == null) return actions;

            a.speed = 2;

            // hide from farmer (indoors or outdoors)
            var farmer = Game1.player;
            if (farmer?.currentLocation == loc)
            {
                var aTile = a.TilePoint.ToVector2();
                var fTile = farmer.Tile;
                float distToFarmer = Vector2.Distance(aTile, fTile);

                if (distToFarmer <= 10f && FindHideSpot(a, farmer, searchRadius: 12, out var hideTile))
                    actions.Add(() => TryHideFromFarmer(a, ctx, hideTile));
            }

            // the rest only make sense outdoors
            if (loc.IsOutdoors)
            {
                var tile = a.TilePoint.ToVector2();
                bool nearGate = HasNearbyGate(loc, tile, 2, out var gateTile);
                bool nearDoor = IsNearAnimalDoor(a, out var doorTile, out _);
                bool adjFence = HasAdjacentFence(loc, tile, out _, out _);

                if (nearGate) actions.Add(() => TryGatePrank(a, ctx, gateTile));
                if (nearDoor) actions.Add(() => TryDoorPrank(a, ctx, doorTile));
                if (adjFence) actions.Add(() => TryFenceHop(a, ctx));
            }

            return actions;
        }

        // ---------- hide from farmer ----------

        private bool TryHideFromFarmer(FarmAnimal a, AIContext ctx, Vector2 hideTile)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 600)) return false; // ~10s
            if (ctx.Rng.NextDouble() >= HideChance) return false;

            if (!_tiles.IsClearTile(a.currentLocation, hideTile)) return false;

            a.speed = Math.Max(a.speed, 3);
            if (FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick)
            {
                FarmAnimal.NumPathfindingThisTick++;
                a.controller = new StardewValley.Pathfinding.PathFindController(a, a.currentLocation, hideTile.ToPoint(), 2);
            }

            EmoteOnScreen(a, 16); // "!"
            
            StardewValley.DelayedAction.functionAfterDelay(() =>
            {
                if (a != null) a.speed = 2;
            }, 2200);

            return true;
        }

        private bool FindHideSpot(FarmAnimal a, Farmer farmer, int searchRadius, out Vector2 hideTile)
        {
            hideTile = default;
            var loc = a.currentLocation;
            if (loc == null) return false;

            Vector2 aTile = a.TilePoint.ToVector2();
            Vector2 fTile = farmer.Tile;

            // scan big craftables
            var coverCandidates = new List<Vector2>();
            foreach (var kv in loc.objects.Pairs)
            {
                if (kv.Value is StardewValley.Object o && o.bigCraftable.Value)
                    coverCandidates.Add(kv.Key);
            }

            // trees outdoors
            if (loc.IsOutdoors)
            {
                foreach (var kv in loc.terrainFeatures.Pairs)
                {
                    if (kv.Value is Tree || kv.Value is FruitTree)
                        coverCandidates.Add(kv.Key);
                }
            }

            // keep nearby
            coverCandidates = coverCandidates
                .Where(c => Vector2.Distance(c, aTile) <= searchRadius)
                .ToList();
            if (coverCandidates.Count == 0) return false;

            // pick a tile on the far side of the cover (relative to farmer)
            Vector2 bestHide = default;
            float bestScore = float.MaxValue;

            foreach (var cover in coverCandidates)
            {
                // choose axis by dominant component so we get a clean orthogonal tile
                Vector2 diff = cover - fTile;
                Vector2 dir = (Math.Abs(diff.X) >= Math.Abs(diff.Y))
                              ? new Vector2(Math.Sign(diff.X), 0)
                              : new Vector2(0, Math.Sign(diff.Y));

                if (dir == Vector2.Zero) continue;

                var candidate = cover + dir; // tile “behind” cover
                if (!loc.isTileOnMap(candidate)) continue;
                if (!_tiles.IsClearTile(loc, candidate)) continue;

                // prefer close to animal but farther from farmer than we are now
                float distAnimal = Vector2.Distance(candidate, aTile);
                float distFarmer = Vector2.Distance(candidate, fTile);
                float currentFarmer = Vector2.Distance(aTile, fTile);
                if (distFarmer <= currentFarmer + 0.5f) continue;

                float score = distAnimal; // simple: nearest hide tile
                if (score < bestScore)
                {
                    bestScore = score;
                    bestHide = candidate;
                }
            }

            if (bestScore < float.MaxValue)
            {
                hideTile = bestHide;
                return true;
            }
            return false;
        }

        // ---------- gates / doors / fence hop ----------

        private bool HasNearbyGate(GameLocation loc, Vector2 centerTile, int r, out Vector2 gateTile)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    Vector2 t = centerTile + new Vector2(dx, dy);
                    if (loc.objects.TryGetValue(t, out var obj) && obj is Fence f && f.isGate.Value)
                    { gateTile = t; return true; }
                }
            gateTile = default; return false;
        }

        private bool HasAdjacentFence(GameLocation loc, Vector2 centerTile, out Vector2 fenceTile, out Vector2 hopTarget)
        {
            Vector2[] dirs = new[]
            {
                new Vector2( 1, 0),
                new Vector2(-1, 0),
                new Vector2( 0, 1),
                new Vector2( 0,-1)
            };

            foreach (var d in dirs)
            {
                var fTile = centerTile + d;
                if (loc.objects.TryGetValue(fTile, out var obj) && obj is Fence)
                {
                    fenceTile = fTile;
                    hopTarget = centerTile + d * 2f;
                    return true;
                }
            }

            fenceTile = default; hopTarget = default; return false;
        }

        private bool IsNearAnimalDoor(FarmAnimal a, out Vector2 doorTile, out float minDistTiles, float radiusTiles = 6f)
        {
            doorTile = default; minDistTiles = float.NaN;

            var home = a.home; var loc = a.currentLocation;
            if (home == null || loc == null || !loc.IsOutdoors) return false;
            if (home.GetParentLocation() != loc) return false;

            int absDoorX = home.tileX.Value + home.animalDoor.X;
            int absDoorY = home.tileY.Value + home.animalDoor.Y;
            Vector2 baseDoor = new(absDoorX, absDoorY);

            Vector2 aTile = a.TilePoint.ToVector2();
            Vector2[] candidates = new[]
            {
                baseDoor,
                baseDoor + new Vector2( 0,  1),
                baseDoor + new Vector2( 0, -1),
                baseDoor + new Vector2( 1,  0),
                baseDoor + new Vector2(-1,  0),
            };

            float best = float.MaxValue;
            Vector2 bestTile = baseDoor;
            foreach (var c in candidates)
            {
                float d = Vector2.Distance(aTile, c);
                if (d < best) { best = d; bestTile = c; }
            }

            doorTile = bestTile;
            minDistTiles = best;
            return best <= radiusTiles;
        }

        private bool TryGatePrank(FarmAnimal a, AIContext ctx, Vector2 hintGateTile)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 600)) return false;
            if (ctx.Rng.NextDouble() >= GatePrankChance) return false;

            var loc = a.currentLocation;
            if (loc.objects.TryGetValue(hintGateTile, out var obj) && obj is Fence f && f.isGate.Value)
            {
                f.gatePosition.Value = f.gatePosition.Value == 0 ? 88 : 0;
                EmoteOnScreen(a, 8);
                PlaySoundIfOnScreen(loc, TileToWorld(hintGateTile), "doorCreak");
                return true;
            }
            return false;
        }

        private bool TryDoorPrank(FarmAnimal a, AIContext ctx, Vector2 doorTile)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 900)) return false;
            if (ctx.Rng.NextDouble() >= DoorPrankChance) return false;
            if (a.home == null) return false;

            bool original = a.home.animalDoorOpen.Value;
            a.home.animalDoorOpen.Value = !original;

            var rect = a.home.getRectForAnimalDoor();
            Vector2 doorWorld = new(rect.Center.X, rect.Center.Y);

            EmoteOnScreen(a, 16);
            PlaySoundIfOnScreen(a.currentLocation, doorWorld, "doorCreak");

            StardewValley.DelayedAction.functionAfterDelay(() =>
            {
                a.home.animalDoorOpen.Value = original;
                PlaySoundIfOnScreen(a.currentLocation, doorWorld, "doorCreak");
                EmoteOnScreen(a, 8);
            }, 2000);

            return true;
        }

        private bool TryFenceHop(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 900)) return false;
            if (ctx.Rng.NextDouble() >= FenceHopChance) return false;

            var loc = a.currentLocation;
            var pos = a.TilePoint.ToVector2();

            if (!HasAdjacentFence(loc, pos, out _, out var hopTarget)) return false;
            if (!_tiles.IsClearTile(loc, hopTarget)) return false;

            // avoid door opening specifically (belt & suspenders)
            var home = a.home;
            if (home != null)
            {
                var dr = home.getRectForAnimalDoor();
                var hw = hopTarget * 64f;
                if (dr.Contains((int)hw.X + 32, (int)hw.Y + 32))
                    return false;
            }

            EmoteOnScreen(a, 16);
            PlaySoundIfOnScreen(loc, a.Position, "dwop");
            StartHop(a, pos, hopTarget, loc);
            return true;
        }

        // ---------- hop helpers ----------

        private static int EstimateHopDurationMs(Vector2 worldDelta)
        {
            float px = Math.Max(Math.Abs(worldDelta.X), Math.Abs(worldDelta.Y));
            return (int)Math.Ceiling((px / 4f) * (1000f / 60f));
        }

        private void StartHop(FarmAnimal a, Vector2 fromTile, Vector2 toTile, GameLocation loc)
        {
            Vector2 deltaTiles = toTile - fromTile;
            Vector2 worldDelta = deltaTiles * 64f;

            int dir;
            if (Math.Abs(deltaTiles.X) >= Math.Abs(deltaTiles.Y)) dir = deltaTiles.X >= 0 ? 1 : 3;
            else dir = deltaTiles.Y >= 0 ? 2 : 0;
            a.faceDirection(dir);

            a.controller = null;
            a.Halt();
            a.hopOffset = worldDelta;

            int ms = EstimateHopDurationMs(worldDelta);
            _hopArc.Start(a, ms, peak: 18);

            StardewValley.DelayedAction.functionAfterDelay(() =>
            {
                if (a == null) return;
                a.setTileLocation(toTile);
                a.yJumpOffset = 0;
                PlaySoundIfOnScreen(loc, TileToWorld(toTile), "thudStep");
                EmoteOnScreen(a, 8);
            }, ms + 30);
        }

        // ---------- small utils ----------

        private static Vector2 TileToWorld(Vector2 tile) => tile * 64f + new Vector2(32f, 32f);

        private static void PlaySoundIfOnScreen(GameLocation loc, Vector2 worldPos, string cue)
        {
            if (loc == Game1.currentLocation && Utility.isOnScreen(worldPos, 64))
                loc.localSound(cue);
        }

        private static void EmoteOnScreen(FarmAnimal a, int emote)
        {
            if (a.currentLocation == Game1.currentLocation && Utility.isOnScreen(a.Position, 128))
                a.doEmote(emote);
        }
    }
}
