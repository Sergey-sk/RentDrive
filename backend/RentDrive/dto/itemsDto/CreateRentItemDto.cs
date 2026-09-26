using Microsoft.AspNetCore.Mvc;
using RentDrive.db.models;

namespace RentDrive.dto.itemsDto
{
    public class CreateRentItemDto
    {
        [FromForm] public string Title { get; set; } = string.Empty;
        [FromForm] public string Description { get; set; } = string.Empty;
        [FromForm] public decimal PricePerDay { get; set; }
        [FromForm] public IFormFileCollection? ImageFiles { get; set; }
    }
}
