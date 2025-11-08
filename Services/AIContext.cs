using StardewModdingAPI;
using StardewValley;
using AnimalPersonalities.Services;

namespace AnimalPersonalities.Services
{
    public class AIContext
    {
        public System.Random Rng { get; set; }
        public IMonitor Monitor { get; set; }
        public IModHelper Helper { get; set; }
        public GameLocation Farm { get; set; }
        public string PersonalityKey { get; set; }
        public EmoteCooldownService Emotes { get; set; }
        public TileService Tiles { get; set; }
    }
}
