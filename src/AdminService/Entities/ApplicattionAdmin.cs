using Microsoft.AspNetCore.Identity;

namespace AdminService.Entities
{
    public class ApplicationAdmin : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string AdminCode { get; set; } = string.Empty;
        public DateTime? AdminCodeExpiry { get; set; }

        // Generate a new admin code
        public void GenerateAdminCode(int expiryHours = 24)
        {
            // Generate a 6-character alphanumeric code
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            AdminCode = new string([.. Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)])]);
            
            AdminCodeExpiry = DateTime.UtcNow.AddHours(expiryHours);
        }

        // Check if admin code is valid
        public bool IsAdminCodeValid(string codeToCheck)
        {
            if (string.IsNullOrEmpty(AdminCode) || AdminCodeExpiry == null)
                return false;

            if (DateTime.UtcNow > AdminCodeExpiry)
                return false;

            return AdminCode.Equals(codeToCheck, StringComparison.OrdinalIgnoreCase);
        }

        // Clear admin code
        public void ClearAdminCode()
        {
            AdminCode = string.Empty;
            AdminCodeExpiry = null;
        }

        // Check if admin code is expired
        public bool IsAdminCodeExpired()
        {
            return AdminCodeExpiry == null || DateTime.UtcNow > AdminCodeExpiry;
        }
    }
}