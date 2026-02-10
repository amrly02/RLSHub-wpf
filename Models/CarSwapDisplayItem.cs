using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RLSHub.Wpf.Models
{
    public sealed class CarSwapDisplayItem : INotifyPropertyChanged
    {
        private bool _isFavorite;

        public CarSwapListing Listing { get; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite == value) return; _isFavorite = value; OnPropertyChanged(); }
        }

        public CarSwapDisplayItem(CarSwapListing listing, bool isFavorite)
        {
            Listing = listing;
            _isFavorite = isFavorite;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
