using AnimalPersonalities.Services;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public class MischievousHandler : IAnimalPersonalityHandler
    {
        private readonly TileService Tiles;
        public MischievousHandler(TileService tiles) => Tiles = tiles;

        public List<Func<bool>> BuildFeasibleActions(FarmAnimal a, AIContext ctx)
        {
            var list = new List<Func<bool>>();
            a.speed = 2;

            var farm = ctx.Farm;
            Vector2 pos = a.TilePoint.ToVector2();

            // gate nearby?
            bool nearGate = false;
            for (int x = -2; x <= 2 && !nearGate; x++)
                for (int y = -2; y <= 2 && !nearGate; y++)
                {
                    Vector2 t = pos + new Vector2(x, y);
                    if (farm.objects.TryGetValue(t, out var o) && o is Fence) nearGate = true;
                }
            if (nearGate) list.Add(() => TryGatePrank(a, ctx));

            // near own door?
            bool nearDoor = false;
            if (a.home != null)
            {
                var d = a.home.animalDoor.Value;
                var doorTile = new Vector2(d.X, d.Y);
                nearDoor = Vector2.Distance(pos, doorTile) <= 8f;
            }
            if (nearDoor) list.Add(() => TryDoorPrank(a, ctx));

            // adjacent fence to hop?
            bool adjacentFence = false;
            for (int ox = -1; ox <= 1 && !adjacentFence; ox++)
                for (int oy = -1; oy <= 1 && !adjacentFence; oy++)
                {
                    if (ox == 0 && oy == 0) continue;
                    Vector2 t = pos + new Vector2(ox, oy);
                    if (farm.objects.TryGetValue(t, out var o) && o is Fence) adjacentFence = true;
                }
            if (adjacentFence) list.Add(() => TryFenceHop(a, ctx));

            return list;
        }

        private bool TryGatePrank(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.01) return false;
            var farm = ctx.Farm;
            Vector2 pos = a.TilePoint.ToVector2();

            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                {
                    Vector2 t = pos + new Vector2(x, y);
                    if (farm.objects.TryGetValue(t, out var o) && o is Fence fence)
                    {
                        fence.gatePosition.Value = fence.gatePosition.Value == 0 ? 88 : 0;
                        a.doEmote(8);
                        StardewValley.Game1.playSound("doorCreak");
                        StardewValley.Game1.showGlobalMessage($"{a.displayName} messed with a gate!");
                        return true;
                    }
                }
            return false;
        }

        private bool TryDoorPrank(FarmAnimal a, AIContext ctx)
        {
            if (a.home == null) return false;
            if (ctx.Rng.NextDouble() >= 0.01) return false;

            var building = a.home;
            bool original = building.animalDoorOpen.Value;

            building.animalDoorOpen.Value = !original;
            StardewValley.Game1.playSound("doorCreak");
            a.doEmote(16);
            StardewValley.Game1.showGlobalMessage($"{a.displayName} nudged the barn door!");

            DelayedAction.functionAfterDelay(() =>
            {
                building.animalDoorOpen.Value = original;
                StardewValley.Game1.playSound("doorCreak");
                a.doEmote(8);
                StardewValley.Game1.showGlobalMessage($"{a.displayName} put the door back like nothing happened...");
            }, 2000);

            return true;
        }

        private bool TryFenceHop(FarmAnimal a, AIContext ctx)
        {
            if (ctx.Rng.NextDouble() >= 0.003) return false;

            var farm = ctx.Farm;
            Vector2 pos = a.TilePoint.ToVector2();

            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                {
                    if (ox == 0 && oy == 0) continue;

                    Vector2 fenceTile = pos + new Vector2(ox, oy);
                    if (farm.objects.TryGetValue(fenceTile, out var o) && o is Fence)
                    {
                        Vector2 hopTarget = fenceTile + new Vector2(System.Math.Sign(ox), System.Math.Sign(oy));
                        if (!Tiles.IsClearTile(farm, hopTarget)) continue;

                        // visual hop
                        a.doEmote(16);
                        StardewValley.Game1.playSound("dwop");

                        float jumpHeight = 60f + (float)ctx.Rng.NextDouble() * 30f;
                        float jumpDuration = 500f;

                        DelayedAction.functionAfterDelay(() =>
                        {
                            a.setTileLocation(hopTarget);
                            StardewValley.Game1.playSound("thudStep");
                        }, (int)(jumpDuration / 2));

                        StardewValley.Game1.delayedActions.Add(new DelayedAction(0, () =>
                        {
                            a.yJumpOffset = 0;
                            a.yJumpVelocity = -jumpHeight / 100f;

                            DelayedAction.functionAfterDelay(() =>
                            {
                                a.yJumpVelocity = jumpHeight / 100f;
                            }, (int)(jumpDuration / 2));

                            DelayedAction.functionAfterDelay(() =>
                            {
                                a.yJumpOffset = 0;
                                a.yJumpVelocity = 0;
                                a.doEmote(8);
                                StardewValley.Game1.showGlobalMessage($"{a.displayName} hopped the fence!");
                            }, (int)jumpDuration);
                        }));

                        return true;
                    }
                }
            return false;
        }
    }
}
