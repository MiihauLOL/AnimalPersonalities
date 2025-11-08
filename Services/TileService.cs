using Microsoft.Xna.Framework;
using StardewValley;
using xTile.Dimensions;

namespace AnimalPersonalities.Services
{
    public class TileService
    {
        public bool IsClearTile(GameLocation loc, Vector2 tile)
        {
            if (!loc.isTileLocationOpen(tile)) return false;
            if (!loc.isTilePassable(new Location((int)tile.X * 64, (int)tile.Y * 64), Game1.viewport)) return false;
            if (loc.terrainFeatures.ContainsKey(tile)) return false;
            if (loc.objects.ContainsKey(tile)) return false;
            return true;
        }
    }
}
