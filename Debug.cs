using System;
using StardewModdingAPI;
using StardewValley;

namespace AnimalPersonalities.Debug
{
    /// Press J to set every FarmAnimal's personality to the next value.
    /// (Lazy → Energetic → Mischievous → Affectionate → Greedy → back to Lazy)
    public sealed class DebugPersonalityCycler : IDisposable
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly string _personalityKey;

        private readonly string[] _order = { "Lazy", "Energetic", "Mischievous", "Affectionate", "Greedy" };
        private int _index = 0;

        public DebugPersonalityCycler(IModHelper helper, IMonitor monitor, string personalityKey)
        {
            _helper = helper;
            _monitor = monitor;
            _personalityKey = personalityKey;

            _helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        public void Dispose()
        {
            _helper.Events.Input.ButtonPressed -= OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.activeClickableMenu != null) return;   // don't fire while menus are open
            if (e.Button != SButton.J) return;

            string next = _order[_index];
            _index = (_index + 1) % _order.Length;

            int count = 0;
            Utility.ForEachLocation(loc =>
            {
                if (loc?.animals != null)
                {
                    foreach (var fa in loc.animals.Values)
                    {
                        fa.modData[_personalityKey] = next;
                        count++;
                    }
                }
                return true;
            });

            _monitor.Log($"[DEBUG] Set {count} farm animals to '{next}'.", LogLevel.Info);
        }
    }
}
