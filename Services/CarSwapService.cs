using RLSHub.Wpf.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RLSHub.Wpf.Services
{
    public sealed class CarSwapService
    {
        private readonly HttpClient _httpClient = new();

        public async Task<(IReadOnlyList<CarSwapListing> Listings, string? Error)> GetListingsAsync()
        {
            if (!TryLoadConfig(out var config, out var error))
                return (Array.Empty<CarSwapListing>(), error ?? "Unable to load CarSwap config.");

            var url = new Uri(new Uri(config.SupabaseUrl), "/rest/v1/listings?select=*&status=eq.available&order=created_at.desc");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("apikey", config.SupabaseKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.SupabaseKey}");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return (Array.Empty<CarSwapListing>(), $"Server returned {response.StatusCode}");
                var json = await response.Content.ReadAsStringAsync();
                var listings = JsonSerializer.Deserialize<List<CarSwapListingDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CarSwapListingDto>();
                var result = new List<CarSwapListing>();
                foreach (var listing in listings)
                {
                    var title = string.IsNullOrWhiteSpace(listing.Title) ? "Untitled listing" : listing.Title;
                    var seller = string.IsNullOrWhiteSpace(listing.SellerName) ? "Unknown seller" : listing.SellerName;
                    var model = string.IsNullOrWhiteSpace(listing.VehicleModel) ? "Unknown model" : listing.VehicleModel;
                    var yearDisplay = listing.VehicleYear.HasValue ? listing.VehicleYear.Value.ToString() : "";
                    var priceDisplay = listing.Price > 0 ? $"${listing.Price:N0}" : "$0";
                    result.Add(new CarSwapListing
                    {
                        Id = listing.Id ?? string.Empty,
                        Title = title,
                        Description = string.IsNullOrWhiteSpace(listing.Description) ? "No description provided." : listing.Description,
                        SellerName = seller,
                        Price = listing.Price,
                        VehicleModel = model,
                        VehicleYear = listing.VehicleYear,
                        Mileage = listing.Mileage ?? 0,
                        Status = listing.Status ?? string.Empty,
                        CreatedAtUtc = listing.CreatedAt,
                        PriceDisplay = priceDisplay,
                        SellerDisplay = seller,
                        ModelDisplay = model,
                        YearDisplay = yearDisplay
                    });
                }
                return (result, null);
            }
            catch (Exception ex)
            {
                return (Array.Empty<CarSwapListing>(), ex.Message);
            }
        }

        private bool TryLoadConfig(out CarSwapConfig config, out string? error)
        {
            config = new CarSwapConfig();
            error = null;
            var path = GetConfigPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "CarSwap config.lua not found.";
                return false;
            }
            var content = File.ReadAllText(path);
            var urlMatch = Regex.Match(content, @"SUPABASE_URL\s*=\s*""(?<value>[^""]+)""");
            var keyMatch = Regex.Match(content, @"SUPABASE_ANON_KEY\s*=\s*""(?<value>[^""]+)""");
            if (!urlMatch.Success || !keyMatch.Success)
            {
                error = "CarSwap config missing SUPABASE_URL or SUPABASE_ANON_KEY.";
                return false;
            }
            config = new CarSwapConfig { SupabaseUrl = urlMatch.Groups["value"].Value, SupabaseKey = keyMatch.Groups["value"].Value };
            return true;
        }

        private string? GetConfigPath()
        {
            if (!BeamNgConfigService.TryLoad(out var beamConfig) || beamConfig == null)
                return null;
            var userRoot = beamConfig.UserFolder;
            var candidatePaths = new[]
            {
                Path.Combine(userRoot, "mods", "unpacked", "rls-career-overhaul", "lua", "ge", "extensions", "gameplay", "carswap", "config.lua"),
                Path.Combine(userRoot, "mods", "rls-career-overhaul", "lua", "ge", "extensions", "gameplay", "carswap", "config.lua")
            };
            foreach (var candidate in candidatePaths)
            {
                if (File.Exists(candidate)) return candidate;
            }
            var packagedPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CarSwap", "config.lua");
            return File.Exists(packagedPath) ? packagedPath : null;
        }

        private sealed class CarSwapConfig
        {
            public string SupabaseUrl { get; set; } = string.Empty;
            public string SupabaseKey { get; set; } = string.Empty;
        }

        private sealed class CarSwapListingDto
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("description")] public string? Description { get; set; }
            [JsonPropertyName("seller_name")] public string? SellerName { get; set; }
            [JsonPropertyName("price")] public int Price { get; set; }
            [JsonPropertyName("vehicle_model")] public string? VehicleModel { get; set; }
            [JsonPropertyName("vehicle_year")] public int? VehicleYear { get; set; }
            [JsonPropertyName("mileage")] public long? Mileage { get; set; }
            [JsonPropertyName("status")] public string? Status { get; set; }
            [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        }
    }
}
