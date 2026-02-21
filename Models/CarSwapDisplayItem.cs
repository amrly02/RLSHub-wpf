using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RLSHub.Wpf.Models
{
    public sealed class CarSwapDisplayItem : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private ImageSource? _thumbnailImage;
        private bool _thumbnailLoaded;
        private List<ImageSource>? _allImages;
        private bool _allImagesLoaded;

        public CarSwapListing Listing { get; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite == value) return; _isFavorite = value; OnPropertyChanged(); }
        }

        public ImageSource? ThumbnailImage
        {
            get
            {
                if (!_thumbnailLoaded)
                {
                    _thumbnailLoaded = true;
                    _thumbnailImage = LoadThumbnail();
                }
                return _thumbnailImage;
            }
        }

        public IReadOnlyList<ImageSource> AllImages
        {
            get
            {
                if (!_allImagesLoaded)
                {
                    _allImagesLoaded = true;
                    _allImages = LoadAllImages();
                }
                return _allImages ?? new List<ImageSource>();
            }
        }

        public CarSwapDisplayItem(CarSwapListing listing, bool isFavorite)
        {
            Listing = listing;
            _isFavorite = isFavorite;
        }

        private ImageSource? LoadThumbnail()
        {
            var images = AllImages;
            return images.Count > 0 ? images[0] : null;
        }

        private List<ImageSource> LoadAllImages()
        {
            var result = new List<ImageSource>();
            var base64Strings = new List<string>();

            // Check if we have separate columns for photos (new format)
            if (!string.IsNullOrWhiteSpace(Listing.Thumbnail2Base64))
            {
                if (!string.IsNullOrWhiteSpace(Listing.ThumbnailBase64)) base64Strings.Add(Listing.ThumbnailBase64.Trim());
                if (!string.IsNullOrWhiteSpace(Listing.Thumbnail2Base64)) base64Strings.Add(Listing.Thumbnail2Base64.Trim());
                if (!string.IsNullOrWhiteSpace(Listing.Thumbnail3Base64)) base64Strings.Add(Listing.Thumbnail3Base64.Trim());
                if (!string.IsNullOrWhiteSpace(Listing.Thumbnail4Base64)) base64Strings.Add(Listing.Thumbnail4Base64.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(Listing.ThumbnailBase64Full))
            {
                // Fallback to parsing the full column as a JSON array (legacy format)
                var t = Listing.ThumbnailBase64Full.Trim();
                if (!t.StartsWith("[") && !t.StartsWith("{"))
                {
                    base64Strings.Add(t);
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(t);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in doc.RootElement.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.String)
                                {
                                    var str = element.GetString();
                                    if (!string.IsNullOrWhiteSpace(str))
                                    {
                                        base64Strings.Add(str);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fallback parsing for single image
                        int dq = t.IndexOf('"', 1);
                        int sq = t.IndexOf('\'', 1);
                        int start = dq > 0 ? (sq > 0 ? Math.Min(dq, sq) : dq) : sq;
                        if (start > 0)
                        {
                            char q = t[start];
                            int end = t.IndexOf(q, start + 1);
                            if (end > start)
                            {
                                var b64 = t.Substring(start + 1, end - start - 1).Replace("\\\"", "\"");
                                if (!string.IsNullOrWhiteSpace(b64))
                                {
                                    base64Strings.Add(b64);
                                }
                            }
                        }
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(Listing.ThumbnailBase64))
            {
                // Fallback to just the single thumbnail
                var t = Listing.ThumbnailBase64.Trim();
                if (!t.StartsWith("[") && !t.StartsWith("{"))
                {
                    base64Strings.Add(t);
                }
                else
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(t);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in doc.RootElement.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.String)
                                {
                                    var str = element.GetString();
                                    if (!string.IsNullOrWhiteSpace(str))
                                    {
                                        base64Strings.Add(str);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fallback parsing for single image
                        int dq = t.IndexOf('"', 1);
                        int sq = t.IndexOf('\'', 1);
                        int start = dq > 0 ? (sq > 0 ? Math.Min(dq, sq) : dq) : sq;
                        if (start > 0)
                        {
                            char q = t[start];
                            int end = t.IndexOf(q, start + 1);
                            if (end > start)
                            {
                                var b64 = t.Substring(start + 1, end - start - 1).Replace("\\\"", "\"");
                                if (!string.IsNullOrWhiteSpace(b64))
                                {
                                    base64Strings.Add(b64);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var b64 in base64Strings)
            {
                var cleanB64 = b64;
                if (cleanB64.StartsWith("data:image"))
                {
                    var commaIndex = cleanB64.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        cleanB64 = cleanB64.Substring(commaIndex + 1);
                    }
                }

                try
                {
                    byte[] imageBytes = Convert.FromBase64String(cleanB64);
                    using var ms = new MemoryStream(imageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    result.Add(bitmap);
                }
                catch
                {
                    // Ignore invalid images
                }
            }

            return result;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
