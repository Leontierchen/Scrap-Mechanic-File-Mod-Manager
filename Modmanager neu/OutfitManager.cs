using System.Runtime.Versioning;
using static Modmanager_neu.Program;

namespace Modmanager_neu
{
    [SupportedOSPlatform("windows")]
    internal class OutfitManager
    {
        public static string GetCurrentOutfit()
        {
            // Logik zum Ändern des Outfits
            return "Aktuelles Outfit";
        }
        public static void SaveOutfit()
        {
            // Logik zum Speichern eines Outfits
            Menu.ShowPlaceholder("Outfit gespeichert.");
        }
        public static void LoadOutfit()
        {
            // Logik zum Laden eines Outfits
            Menu.ShowPlaceholder("Outfit geladen.");
        }
        public static void DeleteOutfit()
        {
            // Logik zum Löschen eines Outfits
            Menu.ShowPlaceholder("Outfit gelöscht.");
        }
    }
}
