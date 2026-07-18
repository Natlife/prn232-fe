using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace CarSalesManagementSystemClient.Helpers
{
    public static class CartHelper
    {
        public static string GetCartFilePath(string userId, IWebHostEnvironment env)
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data", "Carts");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, $"{userId}.json");
        }

        public static void SyncCartOnLogin(string userId, IWebHostEnvironment env, ISession session)
        {
            var filePath = GetCartFilePath(userId, env);
            var guestCartJson = session.GetString("UnifiedCartSession");

            if (!string.IsNullOrEmpty(guestCartJson) && guestCartJson != "{\"Items\":[]}")
            {
                // User built a cart as guest, overwrite their saved cart
                File.WriteAllText(filePath, guestCartJson);
            }
            else
            {
                // No guest cart, load from saved file
                if (File.Exists(filePath))
                {
                    var savedCartJson = File.ReadAllText(filePath);
                    session.SetString("UnifiedCartSession", savedCartJson);
                }
            }
        }

        public static void SaveCartToFile(string userId, IWebHostEnvironment env, string cartJson)
        {
            if (string.IsNullOrEmpty(userId)) return;
            var filePath = GetCartFilePath(userId, env);
            File.WriteAllText(filePath, cartJson);
        }
    }
}
