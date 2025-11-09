using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using xTile.Dimensions;

namespace AnimalPersonalities.Services
{
    public class TileService
    {
        public bool IsClearTile(GameLocation loc, Vector2 tile)
        {
            // on map?
            if (loc == null || !loc.isTileOnMap(tile))
                return false;

            // buildings: if we’re on the Farm, block any tile inside a building footprint
            if (loc is Farm farm)
            {
                foreach (var b in farm.buildings)
                {
                    int bx = b.tileX.Value, by = b.tileY.Value;
                    int bw = b.tilesWide.Value, bh = b.tilesHigh.Value;
                    if (tile.X >= bx && tile.X < bx + bw &&
                        tile.Y >= by && tile.Y < by + bh)
                        return false; // don’t land inside/under a building (door tile included lol)
                }
            }

            // vanilla passability & other blockers
            if (!loc.isTileLocationOpen(tile)) return false;
            if (!loc.isTilePassable(new Location((int)tile.X * 64, (int)tile.Y * 64), Game1.viewport)) return false;
            if (loc.terrainFeatures.ContainsKey(tile)) return false;
            if (loc.objects.ContainsKey(tile)) return false;

            return true;
        }
    }
}
