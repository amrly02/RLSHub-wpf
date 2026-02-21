using System;

namespace RLSHub.Wpf.Models
{
    public sealed class CarSwapListing
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
        public int Price { get; set; }
        public string VehicleModel { get; set; } = string.Empty;
        public int? VehicleYear { get; set; }
        public long Mileage { get; set; }
        public int? Condition { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAtUtc { get; set; }
        public string? ThumbnailBase64 { get; set; }
        public string? ThumbnailBase64Full { get; set; }
        public string? Thumbnail2Base64 { get; set; }
        public string? Thumbnail3Base64 { get; set; }
        public string? Thumbnail4Base64 { get; set; }
        public string PriceDisplay { get; set; } = string.Empty;
        public string SellerDisplay { get; set; } = string.Empty;
        public string ModelDisplay { get; set; } = string.Empty;
        public string YearDisplay { get; set; } = string.Empty;
        public string MileageDisplay { get; set; } = string.Empty;
    }
}
