using Microsoft.AspNetCore.Identity;

namespace RentDrive.db.models
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public List<RentItem> OwnedItems { get; set; } = [];
        public List<Booking> Bookings { get; set; } = [];
    }
}
