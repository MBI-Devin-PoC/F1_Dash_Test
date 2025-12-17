using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace F1McpServer.Tools;

[McpServerToolType]
public static class F1Tools
{
    [McpServerTool, Description("Gets information about the current F1 season including race calendar and upcoming events.")]
    public static string GetSeasonInfo()
    {
        var seasonInfo = new
        {
            Season = 2025,
            TotalRaces = 24,
            CurrentRound = 0,
            NextRace = new
            {
                Name = "Australian Grand Prix",
                Circuit = "Albert Park Circuit",
                Location = "Melbourne, Australia",
                Date = "2025-03-16"
            },
            Teams = new[]
            {
                "Red Bull Racing",
                "Ferrari",
                "Mercedes",
                "McLaren",
                "Aston Martin",
                "Alpine",
                "Williams",
                "RB",
                "Kick Sauber",
                "Haas"
            }
        };

        return JsonSerializer.Serialize(seasonInfo, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets the current driver standings for the F1 championship.")]
    public static string GetDriverStandings()
    {
        var standings = new[]
        {
            new { Position = 1, Driver = "Max Verstappen", Team = "Red Bull Racing", Points = 0, Nationality = "Dutch" },
            new { Position = 2, Driver = "Lando Norris", Team = "McLaren", Points = 0, Nationality = "British" },
            new { Position = 3, Driver = "Charles Leclerc", Team = "Ferrari", Points = 0, Nationality = "Monegasque" },
            new { Position = 4, Driver = "Oscar Piastri", Team = "McLaren", Points = 0, Nationality = "Australian" },
            new { Position = 5, Driver = "Carlos Sainz", Team = "Williams", Points = 0, Nationality = "Spanish" },
            new { Position = 6, Driver = "George Russell", Team = "Mercedes", Points = 0, Nationality = "British" },
            new { Position = 7, Driver = "Lewis Hamilton", Team = "Ferrari", Points = 0, Nationality = "British" },
            new { Position = 8, Driver = "Fernando Alonso", Team = "Aston Martin", Points = 0, Nationality = "Spanish" },
            new { Position = 9, Driver = "Pierre Gasly", Team = "Alpine", Points = 0, Nationality = "French" },
            new { Position = 10, Driver = "Nico Hulkenberg", Team = "Kick Sauber", Points = 0, Nationality = "German" }
        };

        return JsonSerializer.Serialize(standings, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets the constructor (team) standings for the F1 championship.")]
    public static string GetConstructorStandings()
    {
        var standings = new[]
        {
            new { Position = 1, Team = "McLaren", Points = 0, Drivers = new[] { "Lando Norris", "Oscar Piastri" } },
            new { Position = 2, Team = "Ferrari", Points = 0, Drivers = new[] { "Charles Leclerc", "Lewis Hamilton" } },
            new { Position = 3, Team = "Red Bull Racing", Points = 0, Drivers = new[] { "Max Verstappen", "Liam Lawson" } },
            new { Position = 4, Team = "Mercedes", Points = 0, Drivers = new[] { "George Russell", "Andrea Kimi Antonelli" } },
            new { Position = 5, Team = "Aston Martin", Points = 0, Drivers = new[] { "Fernando Alonso", "Lance Stroll" } },
            new { Position = 6, Team = "Alpine", Points = 0, Drivers = new[] { "Pierre Gasly", "Jack Doohan" } },
            new { Position = 7, Team = "Williams", Points = 0, Drivers = new[] { "Carlos Sainz", "Alex Albon" } },
            new { Position = 8, Team = "RB", Points = 0, Drivers = new[] { "Yuki Tsunoda", "Isack Hadjar" } },
            new { Position = 9, Team = "Kick Sauber", Points = 0, Drivers = new[] { "Nico Hulkenberg", "Gabriel Bortoleto" } },
            new { Position = 10, Team = "Haas", Points = 0, Drivers = new[] { "Esteban Ocon", "Oliver Bearman" } }
        };

        return JsonSerializer.Serialize(standings, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets information about a specific F1 circuit by name.")]
    public static string GetCircuitInfo(
        [Description("The name of the circuit to get information about (e.g., 'Monaco', 'Silverstone', 'Spa')")] string circuitName)
    {
        var circuits = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Monaco"] = new
            {
                Name = "Circuit de Monaco",
                Location = "Monte Carlo, Monaco",
                Length = "3.337 km",
                Laps = 78,
                LapRecord = "1:12.909 (Lewis Hamilton, 2021)",
                FirstGrandPrix = 1950,
                Corners = 19
            },
            ["Silverstone"] = new
            {
                Name = "Silverstone Circuit",
                Location = "Silverstone, United Kingdom",
                Length = "5.891 km",
                Laps = 52,
                LapRecord = "1:27.097 (Max Verstappen, 2020)",
                FirstGrandPrix = 1950,
                Corners = 18
            },
            ["Spa"] = new
            {
                Name = "Circuit de Spa-Francorchamps",
                Location = "Stavelot, Belgium",
                Length = "7.004 km",
                Laps = 44,
                LapRecord = "1:46.286 (Valtteri Bottas, 2018)",
                FirstGrandPrix = 1950,
                Corners = 19
            },
            ["Monza"] = new
            {
                Name = "Autodromo Nazionale Monza",
                Location = "Monza, Italy",
                Length = "5.793 km",
                Laps = 53,
                LapRecord = "1:21.046 (Rubens Barrichello, 2004)",
                FirstGrandPrix = 1950,
                Corners = 11
            },
            ["Suzuka"] = new
            {
                Name = "Suzuka International Racing Course",
                Location = "Suzuka, Japan",
                Length = "5.807 km",
                Laps = 53,
                LapRecord = "1:30.983 (Lewis Hamilton, 2019)",
                FirstGrandPrix = 1987,
                Corners = 18
            }
        };

        if (circuits.TryGetValue(circuitName, out var circuit))
        {
            return JsonSerializer.Serialize(circuit, new JsonSerializerOptions { WriteIndented = true });
        }

        return JsonSerializer.Serialize(new
        {
            Error = $"Circuit '{circuitName}' not found",
            AvailableCircuits = circuits.Keys.ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets information about a specific F1 driver by name.")]
    public static string GetDriverInfo(
        [Description("The name of the driver to get information about (e.g., 'Verstappen', 'Hamilton', 'Leclerc')")] string driverName)
    {
        var drivers = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Verstappen"] = new
            {
                FullName = "Max Verstappen",
                Number = 1,
                Team = "Red Bull Racing",
                Nationality = "Dutch",
                DateOfBirth = "1997-09-30",
                WorldChampionships = 4,
                GrandPrixWins = 63,
                PolePositions = 40,
                FastestLaps = 32
            },
            ["Hamilton"] = new
            {
                FullName = "Lewis Hamilton",
                Number = 44,
                Team = "Ferrari",
                Nationality = "British",
                DateOfBirth = "1985-01-07",
                WorldChampionships = 7,
                GrandPrixWins = 105,
                PolePositions = 104,
                FastestLaps = 67
            },
            ["Leclerc"] = new
            {
                FullName = "Charles Leclerc",
                Number = 16,
                Team = "Ferrari",
                Nationality = "Monegasque",
                DateOfBirth = "1997-10-16",
                WorldChampionships = 0,
                GrandPrixWins = 8,
                PolePositions = 26,
                FastestLaps = 9
            },
            ["Norris"] = new
            {
                FullName = "Lando Norris",
                Number = 4,
                Team = "McLaren",
                Nationality = "British",
                DateOfBirth = "1999-11-13",
                WorldChampionships = 0,
                GrandPrixWins = 4,
                PolePositions = 9,
                FastestLaps = 9
            },
            ["Russell"] = new
            {
                FullName = "George Russell",
                Number = 63,
                Team = "Mercedes",
                Nationality = "British",
                DateOfBirth = "1998-02-15",
                WorldChampionships = 0,
                GrandPrixWins = 3,
                PolePositions = 5,
                FastestLaps = 7
            }
        };

        if (drivers.TryGetValue(driverName, out var driver))
        {
            return JsonSerializer.Serialize(driver, new JsonSerializerOptions { WriteIndented = true });
        }

        return JsonSerializer.Serialize(new
        {
            Error = $"Driver '{driverName}' not found",
            AvailableDrivers = drivers.Keys.ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets the race calendar for the F1 season with all scheduled races.")]
    public static string GetRaceCalendar()
    {
        var calendar = new[]
        {
            new { Round = 1, Name = "Australian Grand Prix", Circuit = "Albert Park", Date = "2025-03-16", Country = "Australia" },
            new { Round = 2, Name = "Chinese Grand Prix", Circuit = "Shanghai International Circuit", Date = "2025-03-23", Country = "China" },
            new { Round = 3, Name = "Japanese Grand Prix", Circuit = "Suzuka", Date = "2025-04-06", Country = "Japan" },
            new { Round = 4, Name = "Bahrain Grand Prix", Circuit = "Bahrain International Circuit", Date = "2025-04-13", Country = "Bahrain" },
            new { Round = 5, Name = "Saudi Arabian Grand Prix", Circuit = "Jeddah Corniche Circuit", Date = "2025-04-20", Country = "Saudi Arabia" },
            new { Round = 6, Name = "Miami Grand Prix", Circuit = "Miami International Autodrome", Date = "2025-05-04", Country = "USA" },
            new { Round = 7, Name = "Emilia Romagna Grand Prix", Circuit = "Imola", Date = "2025-05-18", Country = "Italy" },
            new { Round = 8, Name = "Monaco Grand Prix", Circuit = "Circuit de Monaco", Date = "2025-05-25", Country = "Monaco" },
            new { Round = 9, Name = "Spanish Grand Prix", Circuit = "Circuit de Barcelona-Catalunya", Date = "2025-06-01", Country = "Spain" },
            new { Round = 10, Name = "Canadian Grand Prix", Circuit = "Circuit Gilles Villeneuve", Date = "2025-06-15", Country = "Canada" },
            new { Round = 11, Name = "Austrian Grand Prix", Circuit = "Red Bull Ring", Date = "2025-06-29", Country = "Austria" },
            new { Round = 12, Name = "British Grand Prix", Circuit = "Silverstone", Date = "2025-07-06", Country = "United Kingdom" },
            new { Round = 13, Name = "Belgian Grand Prix", Circuit = "Spa-Francorchamps", Date = "2025-07-27", Country = "Belgium" },
            new { Round = 14, Name = "Hungarian Grand Prix", Circuit = "Hungaroring", Date = "2025-08-03", Country = "Hungary" },
            new { Round = 15, Name = "Dutch Grand Prix", Circuit = "Zandvoort", Date = "2025-08-31", Country = "Netherlands" },
            new { Round = 16, Name = "Italian Grand Prix", Circuit = "Monza", Date = "2025-09-07", Country = "Italy" },
            new { Round = 17, Name = "Azerbaijan Grand Prix", Circuit = "Baku City Circuit", Date = "2025-09-21", Country = "Azerbaijan" },
            new { Round = 18, Name = "Singapore Grand Prix", Circuit = "Marina Bay Street Circuit", Date = "2025-10-05", Country = "Singapore" },
            new { Round = 19, Name = "United States Grand Prix", Circuit = "Circuit of the Americas", Date = "2025-10-19", Country = "USA" },
            new { Round = 20, Name = "Mexico City Grand Prix", Circuit = "Autodromo Hermanos Rodriguez", Date = "2025-10-26", Country = "Mexico" },
            new { Round = 21, Name = "Brazilian Grand Prix", Circuit = "Interlagos", Date = "2025-11-09", Country = "Brazil" },
            new { Round = 22, Name = "Las Vegas Grand Prix", Circuit = "Las Vegas Strip Circuit", Date = "2025-11-22", Country = "USA" },
            new { Round = 23, Name = "Qatar Grand Prix", Circuit = "Lusail International Circuit", Date = "2025-11-30", Country = "Qatar" },
            new { Round = 24, Name = "Abu Dhabi Grand Prix", Circuit = "Yas Marina Circuit", Date = "2025-12-07", Country = "UAE" }
        };

        return JsonSerializer.Serialize(calendar, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Calculates championship points based on race position.")]
    public static string CalculatePoints(
        [Description("The finishing position in the race (1-20)")] int position,
        [Description("Whether the driver set the fastest lap (true/false)")] bool fastestLap = false,
        [Description("Whether this is a sprint race (true/false)")] bool isSprint = false)
    {
        int[] racePoints = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
        int[] sprintPoints = { 8, 7, 6, 5, 4, 3, 2, 1 };

        int points = 0;
        string raceType = isSprint ? "Sprint" : "Grand Prix";

        if (isSprint)
        {
            if (position >= 1 && position <= 8)
            {
                points = sprintPoints[position - 1];
            }
        }
        else
        {
            if (position >= 1 && position <= 10)
            {
                points = racePoints[position - 1];
            }

            if (fastestLap && position <= 10)
            {
                points += 1;
            }
        }

        var result = new
        {
            RaceType = raceType,
            Position = position,
            BasePoints = isSprint ? (position <= 8 ? sprintPoints[position - 1] : 0) : (position <= 10 ? racePoints[position - 1] : 0),
            FastestLapBonus = (!isSprint && fastestLap && position <= 10) ? 1 : 0,
            TotalPoints = points
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gets tire compound information and their characteristics.")]
    public static string GetTireInfo(
        [Description("The tire compound to get information about (soft, medium, hard, intermediate, wet)")] string compound = "all")
    {
        var tires = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["soft"] = new
            {
                Compound = "Soft",
                Color = "Red",
                Grip = "Highest",
                Durability = "Lowest",
                OptimalTemperature = "90-110°C",
                Description = "Maximum grip for qualifying and short stints. Degrades quickly under heavy load."
            },
            ["medium"] = new
            {
                Compound = "Medium",
                Color = "Yellow",
                Grip = "Medium",
                Durability = "Medium",
                OptimalTemperature = "85-105°C",
                Description = "Balanced performance between grip and durability. Versatile choice for race strategy."
            },
            ["hard"] = new
            {
                Compound = "Hard",
                Color = "White",
                Grip = "Lowest",
                Durability = "Highest",
                OptimalTemperature = "80-100°C",
                Description = "Maximum durability for long stints. Lower initial grip but consistent performance."
            },
            ["intermediate"] = new
            {
                Compound = "Intermediate",
                Color = "Green",
                Grip = "High (wet)",
                Durability = "Medium",
                OptimalTemperature = "30-50°C",
                Description = "For damp or drying track conditions. Clears light water while maintaining grip."
            },
            ["wet"] = new
            {
                Compound = "Wet",
                Color = "Blue",
                Grip = "High (heavy rain)",
                Durability = "High",
                OptimalTemperature = "25-45°C",
                Description = "For heavy rain conditions. Maximum water displacement for standing water."
            }
        };

        if (compound.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(tires.Values, new JsonSerializerOptions { WriteIndented = true });
        }

        if (tires.TryGetValue(compound, out var tire))
        {
            return JsonSerializer.Serialize(tire, new JsonSerializerOptions { WriteIndented = true });
        }

        return JsonSerializer.Serialize(new
        {
            Error = $"Tire compound '{compound}' not found",
            AvailableCompounds = tires.Keys.ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
