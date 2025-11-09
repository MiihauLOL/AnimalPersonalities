using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewModdingAPI;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    // Mischievous: pick at most one feasible action per check.
    public class MischievousHandler : IAnimalPersonalityHandler
    {
        private readonly TileService _tiles;
        private readonly IMonitor _monitor;
        private readonly HopArcService _hopArc;

        // tweakable odds
        private const double GatePrankChance = 0.01;  // 1%
        private const double DoorPrankChance = 0.01;  // 1%
        private const double FenceHopChance = 0.01;   // 1%

        public MischievousHandler(TileService tiles, IMonitor monitor, HopArcService hopArc)
        { _tiles = tiles; _monitor = monitor; _hopArc = hopArc; }

        // per-animal anti-spam
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
            if (loc?.IsOutdoors != true) return actions;

            a.speed = 2;

            // figure out what we *could* do; ModEntry will roll one
            var tile = a.TilePoint.ToVector2();
            bool nearGate = HasNearbyGate(loc, tile, 2, out var gateTile);
            bool nearDoor = IsNearAnimalDoor(a, out var doorTile, out _);
            bool adjFence = HasAdjacentFence(loc, tile, out var _, out var _);

            if (nearGate) actions.Add(() => TryGatePrank(a, ctx, gateTile));
            if (nearDoor) actions.Add(() => TryDoorPrank(a, ctx, doorTile));
            if (adjFence) actions.Add(() => TryFenceHop(a, ctx));

            return actions;
        }

        // scan a small radius for a gate
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

        // orthogonal fences only; land exactly two tiles past the fence
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

        // door tile = building origin + door offset; check door + 4 neighbors
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
                ToggleGate(f);
                EmoteOnScreen(a, 8);
                PlaySoundIfOnScreen(loc, TileToWorld(hintGateTile), "doorCreak");
                return true;
            }
            return false;
        }

        // toggle barn door and revert; use the door rect for world sound
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

            DelayedAction.functionAfterDelay(() =>
            {
                a.home.animalDoorOpen.Value = original;
                PlaySoundIfOnScreen(a.currentLocation, doorWorld, "doorCreak");
                EmoteOnScreen(a, 8);
            }, 2000);

            return true;
        }

        // hop across one fence; real hop + arc
        private bool TryFenceHop(FarmAnimal a, AIContext ctx)
        {
            if (!CooldownOK(_cooldown, a.myID.Value, 900)) return false;
            if (ctx.Rng.NextDouble() >= FenceHopChance) return false;

            var loc = a.currentLocation;
            var pos = a.TilePoint.ToVector2();

            if (!HasAdjacentFence(loc, pos, out var _, out var hopTarget)) return false;
            if (!_tiles.IsClearTile(loc, hopTarget)) return false;

            EmoteOnScreen(a, 16);
            PlaySoundIfOnScreen(loc, a.Position, "dwop");
            StartHop(a, pos, hopTarget, loc);
            return true;
        }

        private static void ToggleGate(Fence fence)
        {
            fence.gatePosition.Value = fence.gatePosition.Value == 0 ? 88 : 0;
        }

        // vanilla hop moves ~4px/update @ ~60fps; convert to ms
        private static int EstimateHopDurationMs(Vector2 worldDelta)
        {
            float px = Math.Max(Math.Abs(worldDelta.X), Math.Abs(worldDelta.Y));
            return (int)Math.Ceiling((px / 4f) * (1000f / 60f));
        }

        private void StartHop(FarmAnimal a, Vector2 fromTile, Vector2 toTile, GameLocation loc)
        {
            Vector2 deltaTiles = toTile - fromTile;
            Vector2 worldDelta = deltaTiles * 64f;

            // face hop direction
            int dir;
            if (Math.Abs(deltaTiles.X) >= Math.Abs(deltaTiles.Y)) dir = deltaTiles.X >= 0 ? 1 : 3;
            else dir = deltaTiles.Y >= 0 ? 2 : 0;
            a.faceDirection(dir);

            a.controller = null;
            a.Halt();
            a.hopOffset = worldDelta; // slide is handled by vanilla

            int ms = EstimateHopDurationMs(worldDelta);
            _hopArc.Start(a, ms, peak: 30); // per-tick arc

            DelayedAction.functionAfterDelay(() =>
            {
                if (a == null) return;
                a.setTileLocation(toTile);
                a.yJumpOffset = 0;
                PlaySoundIfOnScreen(loc, TileToWorld(toTile), "thudStep");
                EmoteOnScreen(a, 8);
            }, ms + 30);
        }

        private static Vector2 TileToWorld(Vector2 tile) => tile * 64f + new Vector2(32f, 32f);

        // screen-gated fx
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
