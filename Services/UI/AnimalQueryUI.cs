using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Characters;

namespace AnimalPersonalities.Services.UI
{
    public class AnimalQueryUI
    {
        private readonly string _personalityKey;

        public AnimalQueryUI(string personalityKey, IModHelper _)
        {
            _personalityKey = personalityKey;
        }

        public void OnRenderedMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            if (Game1.activeClickableMenu is not AnimalQueryMenu menu)
                return;

            // only draw on the main info view (not while placing/moving)
            if (menu.movingAnimal || Game1.globalFade)
                return;

            FarmAnimal animal = menu.animal;
            if (animal == null)
                return;

            if (!animal.modData.TryGetValue(_personalityKey, out string personality))
                return;

            // position inside the window
            Vector2 pos = new(
                menu.xPositionOnScreen + AnimalQueryMenu.width / 8,
                menu.yPositionOnScreen + AnimalQueryMenu.height - 80
            );

            bool canSee = CanSeePersonality(Game1.player, animal);
            string text = canSee ? $"Personality: {personality}" : "Personality: ???";

            //e.SpriteBatch.DrawString(Game1.smallFont, text, pos, Game1.textColor);
            Utility.drawTextWithShadow(e.SpriteBatch, text, Game1.smallFont, pos, Game1.textColor);

            // hover tooltip for locked state
            if (!canSee)
            {
                var size = Game1.smallFont.MeasureString(text);
                var rect = new Rectangle((int)pos.X, (int)pos.Y, (int)System.Math.Ceiling(size.X), (int)System.Math.Ceiling(size.Y));
                if (rect.Contains(Game1.getMouseX(), Game1.getMouseY()))
                {
                    IClickableMenu.drawHoverText(
                        e.SpriteBatch,
                        "More friendship needed to see personality",
                        Game1.smallFont
                    );
                }
            }

            // draw the mouse AGAIN so it's above text
            menu.drawMouse(e.SpriteBatch);
        }

        private bool CanSeePersonality(Farmer farmer, FarmAnimal animal)
        {
            int hearts = (int)System.Math.Floor(animal.friendshipTowardFarmer.Value / 200f);

            bool hasShepherd = farmer.professions.Contains(Farmer.shepherd);
            bool hasButcher = farmer.professions.Contains(Farmer.butcher); // Coopmaster id
            if (hasShepherd || hasButcher) return true;

            bool hasRancher = farmer.professions.Contains(Farmer.rancher);
            int threshold = hasRancher ? 1 : 2;
            return hearts >= threshold;
        }
    }
}
