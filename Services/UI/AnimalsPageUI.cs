using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Characters;

namespace AnimalPersonalities.Services.UI
{
    public class AnimalPageUI
    {
        private readonly string _personalityKey;

        public AnimalPageUI(string personalityKey, IModHelper _)
        {
            _personalityKey = personalityKey;
        }

        public void OnRenderedMenu(object sender, StardewModdingAPI.Events.RenderedActiveMenuEventArgs e)
        {
            if (Game1.activeClickableMenu is not GameMenu gm)
                return;

            var pages = gm.pages;
            int tab = gm.currentTab;
            if (pages == null || tab < 0 || tab >= pages.Count) return;
            if (pages[tab] is not AnimalPage page) return;

            int first = page.slotPosition;
            int last = Math.Min(first + AnimalPage.slotsOnPage - 1, page.sprites.Count - 1);

            for (int i = first; i <= last; i++)
            {
                var entry = page.GetSocialEntry(i);
                if (entry == null) continue;

                if (entry.Animal is not FarmAnimal fa)
                    continue; // pets/horses: no label

                // --- same center math AnimalPage uses for the name ---
                float nameCenterX =
                    page.xPositionOnScreen
                    + IClickableMenu.borderWidth * 3 / 2
                    + 192 - 20 + 96;

                int num2 = (entry.TextureSourceRect.Height <= 16) ? -40 : 8;
                float wH = Game1.smallFont.MeasureString("W").Y;
                float num = (LocalizedContentManager.CurrentLanguageCode is LocalizedContentManager.LanguageCode.ru
                             or LocalizedContentManager.LanguageCode.ko) ? (-wH / 2f) : 0f;
                float nameY = page.sprites[i].bounds.Y + 48 + num2 + num - 20f;

                bool canSee = CanSeePersonality(Game1.player, fa);
                string text = (canSee && fa.modData.TryGetValue(_personalityKey, out string p))
                                ? $"Personality: {p}"
                                : "Personality: ???";

                // centered under the name; +42f looked perfect for you
                float textWidth = Game1.smallFont.MeasureString(text).X;
                Vector2 pos = new(nameCenterX - textWidth / 2f, nameY + 42f);

                e.SpriteBatch.DrawString(Game1.smallFont, text, pos, Color.Black);

                // hover hint for locked state
                if (!canSee)
                {
                    int mx = Game1.getMouseX();
                    int my = Game1.getMouseY();
                    float textHeight = Game1.smallFont.MeasureString(text).Y;

                    var rect = new Rectangle((int)pos.X, (int)pos.Y, (int)Math.Ceiling(textWidth), (int)Math.Ceiling(textHeight));
                    if (rect.Contains(mx, my))
                    {
                        IClickableMenu.drawHoverText(
                            e.SpriteBatch,
                            "More friendship needed to see personality",
                            Game1.smallFont
                        );
                    }
                }
            }
        }

        private bool CanSeePersonality(Farmer farmer, FarmAnimal animal)
        {
            int hearts = (int)Math.Floor(animal.friendshipTowardFarmer.Value / 200f);

            bool hasShepherd = farmer.professions.Contains(Farmer.shepherd);
            bool hasButcher = farmer.professions.Contains(Farmer.butcher); // Coopmaster id
            if (hasShepherd || hasButcher) return true;

            bool hasRancher = farmer.professions.Contains(Farmer.rancher);
            int threshold = hasRancher ? 1 : 2;
            return hearts >= threshold;
        }
    }
}
