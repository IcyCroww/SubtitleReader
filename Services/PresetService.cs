using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

public class PresetService
{
    private readonly string _presetsDirectory;
    private readonly string _presetsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new RectJsonConverter() }
    };

    public PresetService()
    {
        _presetsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SubtitleReader",
            "Presets"
        );
        _presetsFilePath = Path.Combine(_presetsDirectory, "presets.json");

        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_presetsDirectory))
        {
            Directory.CreateDirectory(_presetsDirectory);
        }
    }

    public List<GamePreset> LoadPresets()
    {
        if (!File.Exists(_presetsFilePath))
            return new List<GamePreset>();

        try
        {
            var json = File.ReadAllText(_presetsFilePath);
            var presets = JsonSerializer.Deserialize<List<GamePreset>>(json, JsonOptions);
            return presets ?? new List<GamePreset>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки заготовок: {ex.Message}");
            return new List<GamePreset>();
        }
    }

    public void SavePresets(List<GamePreset> presets)
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(presets, JsonOptions);
            File.WriteAllText(_presetsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения заготовок: {ex.Message}");
            throw new Exception($"Ошибка при сохранении заготовок: {ex.Message}", ex);
        }
    }

    public void SavePreset(GamePreset preset)
    {
        var presets = LoadPresets();
        var existing = presets.FirstOrDefault(p => p.Id == preset.Id);
        
        if (existing != null)
        {
            var index = presets.IndexOf(existing);
            presets[index] = preset;
        }
        else
        {
            presets.Add(preset);
        }

        SavePresets(presets);
    }

    public void DeletePreset(string presetId)
    {
        var presets = LoadPresets();
        presets.RemoveAll(p => p.Id == presetId);
        SavePresets(presets);
    }

    public GamePreset? GetPreset(string presetId)
    {
        var presets = LoadPresets();
        return presets.FirstOrDefault(p => p.Id == presetId);
    }
}

/// <summary>
/// Конвертер для сериализации System.Windows.Rect
/// </summary>
public class RectJsonConverter : JsonConverter<Rect>
{
    public override Rect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        double x = 0, y = 0, width = 0, height = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLower())
                {
                    case "x":
                        x = reader.GetDouble();
                        break;
                    case "y":
                        y = reader.GetDouble();
                        break;
                    case "width":
                        width = reader.GetDouble();
                        break;
                    case "height":
                        height = reader.GetDouble();
                        break;
                }
            }
        }

        return new Rect(x, y, width, height);
    }

    public override void Write(Utf8JsonWriter writer, Rect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }
}
