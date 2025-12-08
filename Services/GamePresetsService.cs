using System.Collections.Generic;
using System.Windows;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

/// <summary>
/// Готовые пресеты для популярных игр
/// </summary>
public static class GamePresetsService
{
    public static List<GamePreset> GetBuiltInPresets()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        return new List<GamePreset>
        {
            // Minecraft
            new GamePreset
            {
                Name = "Minecraft",
                Description = "Чат и субтитры Minecraft",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Чат",
                        Bounds = new Rect(10, screenHeight * 0.65, screenWidth * 0.4, screenHeight * 0.25),
                        ReadingSpeed = 1.5,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Субтитры",
                        Bounds = new Rect(screenWidth * 0.7, screenHeight * 0.4, screenWidth * 0.28, screenHeight * 0.2),
                        ReadingSpeed = 1.8,
                        AutoRead = true
                    }
                }
            },

            // RPG игры (Skyrim, Witcher и т.д.)
            new GamePreset
            {
                Name = "RPG (Skyrim, Witcher)",
                Description = "Диалоги и субтитры для RPG игр",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Диалоги",
                        Bounds = new Rect(screenWidth * 0.1, screenHeight * 0.75, screenWidth * 0.8, screenHeight * 0.2),
                        ReadingSpeed = 1.5,
                        AutoRead = true
                    }
                }
            },

            // Визуальные новеллы
            new GamePreset
            {
                Name = "Визуальная новелла",
                Description = "Текстовое окно внизу экрана",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Текст",
                        Bounds = new Rect(screenWidth * 0.05, screenHeight * 0.7, screenWidth * 0.9, screenHeight * 0.28),
                        ReadingSpeed = 1.3,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Имя персонажа",
                        Bounds = new Rect(screenWidth * 0.05, screenHeight * 0.65, screenWidth * 0.3, screenHeight * 0.05),
                        ReadingSpeed = 1.5,
                        AutoRead = false
                    }
                }
            },

            // Стратегии
            new GamePreset
            {
                Name = "Стратегия (Civilization, Total War)",
                Description = "Уведомления и подсказки",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Уведомления",
                        Bounds = new Rect(screenWidth * 0.7, 10, screenWidth * 0.28, screenHeight * 0.15),
                        ReadingSpeed = 1.8,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Подсказки",
                        Bounds = new Rect(screenWidth * 0.3, screenHeight * 0.85, screenWidth * 0.4, screenHeight * 0.12),
                        ReadingSpeed = 1.5,
                        AutoRead = true
                    }
                }
            },

            // Шутеры
            new GamePreset
            {
                Name = "Шутер (CS2, Valorant)",
                Description = "Чат и килфид",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Чат",
                        Bounds = new Rect(10, screenHeight * 0.6, screenWidth * 0.3, screenHeight * 0.3),
                        ReadingSpeed = 2.0,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Килфид",
                        Bounds = new Rect(screenWidth * 0.7, 10, screenWidth * 0.28, screenHeight * 0.2),
                        ReadingSpeed = 2.0,
                        AutoRead = false
                    }
                }
            },

            // Аниме игры
            new GamePreset
            {
                Name = "Аниме игра (Genshin, HSR)",
                Description = "Диалоги и квесты",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Диалоги",
                        Bounds = new Rect(screenWidth * 0.15, screenHeight * 0.78, screenWidth * 0.7, screenHeight * 0.18),
                        ReadingSpeed = 1.5,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Квест",
                        Bounds = new Rect(screenWidth * 0.02, screenHeight * 0.15, screenWidth * 0.25, screenHeight * 0.1),
                        ReadingSpeed = 1.3,
                        AutoRead = false
                    }
                }
            },

            // Универсальный
            new GamePreset
            {
                Name = "Универсальный",
                Description = "Стандартные области для любой игры",
                Regions = new List<TextRegion>
                {
                    new TextRegion
                    {
                        Name = "Нижние субтитры",
                        Bounds = new Rect(screenWidth * 0.1, screenHeight * 0.85, screenWidth * 0.8, screenHeight * 0.12),
                        ReadingSpeed = 1.5,
                        AutoRead = true
                    },
                    new TextRegion
                    {
                        Name = "Верхние уведомления",
                        Bounds = new Rect(screenWidth * 0.6, 10, screenWidth * 0.38, screenHeight * 0.1),
                        ReadingSpeed = 1.8,
                        AutoRead = true
                    }
                }
            }
        };
    }
}
