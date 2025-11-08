using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace AnimalPersonalities.Services
{
    public class PersonalityAssigner
    {
        private static readonly string[] All = { "Lazy", "Energetic", "Mischievous", "Affectionate", "Greedy" };

        public void AssignIfMissing(FarmAnimal a, System.Random rng, string key, IMonitor monitor, bool toast)
        {
            if (a.modData.ContainsKey(key)) return;

            string p = All[rng.Next(All.Length)];
            a.modData[key] = p;
            if (toast)
                StardewValley.Game1.showGlobalMessage($"{a.displayName} seems {p.ToLower()}!");
            monitor.Log($"{a.Name} is now {p}.", LogLevel.Trace);
        }
    }
}
