using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImageToIcon.Models;

public class SizeToggle(int size, bool isChecked, bool isCustom = false) : INotifyPropertyChanged
{
    private bool _isAvailable = true;
    private bool _isChecked = isChecked;

    public int Size { get; } = size;
    public bool IsCustom { get; } = isCustom;
    public string Label => Size.ToString();

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise();
        }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value) return;
            _isAvailable = value;
            Raise();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}