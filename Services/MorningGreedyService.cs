using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations; // ✅ add this

namespace AnimalPersonalities.Services
{
    public class MorningGreedyService
    {
        public void Apply(Farm farm, string personalityKey, System.Random rng, IMonitor monitor)
        {
            int totalHay = farm.piecesOfHay.Value;

            foreach (var b in farm.buildings)
            {
                if (b.indoors.Value is not AnimalHouse house) continue; 

                foreach (var a in house.animals.Values)
                {
                    if (!a.modData.TryGetValue(personalityKey, out string p)) continue;
                    if (p == "Greedy" && rng.NextDouble() < 0.15)
                    {
                        if (totalHay > 0)
                        {
                            totalHay--;
                            Game1.showGlobalMessage($"{a.displayName} devoured an extra serving of hay!");
                        }
                        else
                        {
                            monitor.Log($"{a.Name} tried to eat more hay but there was none left.", LogLevel.Trace);
                        }
                    }
                }
            }

            farm.piecesOfHay.Value = System.Math.Max(0, totalHay);
        }
    }
}
