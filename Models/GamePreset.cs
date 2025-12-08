using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SubtitleReader.Models;

public class GamePreset : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Новая заготовка";
    private string _description = string.Empty;
    private string _gameName = string.Empty;
    private List<TextRegion> _regions = new();
    private DateTime _createdAt = DateTime.Now;
    private DateTime _modifiedAt = DateTime.Now;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string GameName
    {
        get => _gameName;
        set => SetField(ref _gameName, value);
    }

    public List<TextRegion> Regions
    {
        get => _regions;
        set => SetField(ref _regions, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set => SetField(ref _modifiedAt, value);
    }

    public GamePreset Clone()
    {
        return new GamePreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (копия)",
            Description = Description,
            GameName = GameName,
            Regions = Regions.ConvertAll(r => r.Clone()),
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
