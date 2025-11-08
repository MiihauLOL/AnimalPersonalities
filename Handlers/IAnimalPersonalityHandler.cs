using AnimalPersonalities.Services;
using StardewValley;
using StardewValley.Characters;
using System;
using System.Collections.Generic;

namespace AnimalPersonalities.Handlers
{
    public interface IAnimalPersonalityHandler
    {
        /// Build feasible actions first; each action returns true if it actually fired.
        List<Func<bool>> BuildFeasibleActions(FarmAnimal animal, AIContext ctx);
    }
}
