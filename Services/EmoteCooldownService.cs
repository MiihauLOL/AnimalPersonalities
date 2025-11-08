using StardewValley;
using StardewValley.Characters;

namespace AnimalPersonalities.Services
{
    public class EmoteCooldownService
    {
        private const string LastEmoteKey = "AnimalPersonalities/LastEmote";

        public bool CanDoEmote(FarmAnimal a)
        {
            if (!a.modData.TryGetValue(LastEmoteKey, out string ticksString))
                return true;

            if (long.TryParse(ticksString, out long lastTick))
                return StardewValley.Game1.ticks - lastTick > 600; // ~10s

            return true;
        }

        public void SetEmote(FarmAnimal a)
        {
            a.modData[LastEmoteKey] = StardewValley.Game1.ticks.ToString();
        }
    }
}
