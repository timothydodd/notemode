using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flit.ViewModels;

public class StatusBarViewModel : INotifyPropertyChanged
{
    private int _line = 1;
    private int _column = 1;
    private string _encoding = "UTF-8";
    private string _lineEnding = "LF";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Line
    {
        get => _line;
        set
        {
            if (_line != value)
            {
                _line = value;
                OnPropertyChanged();
            }
        }
    }

    public int Column
    {
        get => _column;
        set
        {
            if (_column != value)
            {
                _column = value;
                OnPropertyChanged();
            }
        }
    }

    public string Encoding
    {
        get => _encoding;
        set
        {
            if (_encoding != value)
            {
                _encoding = value;
                OnPropertyChanged();
            }
        }
    }

    public string LineEnding
    {
        get => _lineEnding;
        set
        {
            if (_lineEnding != value)
            {
                _lineEnding = value;
                OnPropertyChanged();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
