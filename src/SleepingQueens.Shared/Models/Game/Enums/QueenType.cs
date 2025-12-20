using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueenType
{
    RoseQueen = 5,
    StarfishQueen = 5,
    CakeQueen = 5,
    RainbowQueen = 5,
    PeacockQueen = 10,
    MoonQueen = 10,
    SunflowerQueen = 10,
    LadybugQueen = 10,
    CatQueen = 15,
    DogQueen = 15,
    PancakeQueen = 15,
    HeartQueen = 20
}
