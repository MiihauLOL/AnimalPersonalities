using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AnimalPersonalities.Services
{
    public class HopArcService
    {
        private readonly IMonitor _monitor;
        private readonly Dictionary<long, (WeakReference<FarmAnimal> wr, int startTick, int durTicks, int peak)> _arcs = new();

        public HopArcService(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked; // per-frame
        }

        
        public void Start(FarmAnimal a, int durationMs, int peak = 30)
        {
            if (a == null) return;
            int durTicks = Math.Max(1, (int)Math.Ceiling(durationMs / (1000f / 60f)));
            _arcs[a.myID.Value] = (new WeakReference<FarmAnimal>(a), Game1.ticks, durTicks, peak);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_arcs.Count == 0) return;

            var done = new List<long>();
            foreach (var kvp in _arcs)
            {
                var id = kvp.Key;
                var (wr, startTick, durTicks, peak) = kvp.Value;

                if (!wr.TryGetTarget(out var a) || a?.currentLocation == null)
                {
                    done.Add(id);
                    continue;
                }

                float t = (Game1.ticks - startTick) / (float)durTicks; 
                if (t >= 1f)
                {
                    a.yJumpOffset = 0;
                    done.Add(id);
                }
                else
                {
                    // smooth arc (sinus), negative to go up on screen
                    a.yJumpOffset = -(int)(Math.Sin(MathF.PI * t) * peak);
                }
            }

            foreach (var id in done) _arcs.Remove(id);
        }
    }
}
