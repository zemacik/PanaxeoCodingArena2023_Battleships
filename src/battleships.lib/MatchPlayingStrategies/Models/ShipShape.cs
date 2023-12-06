namespace battleships.lib.MatchPlayingStrategies.Models;

public class ShipShape
{
    public bool[,] Shape { get; }

    private ShipShape(bool[,] shape)
    {
        Shape = shape;
    }

    public ShipShape Rotate()
    {
        var rows = Shape.GetLength(0);
        var cols = Shape.GetLength(1);

        var rotatedShape = new bool[cols, rows];

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            rotatedShape[c, r] = Shape[r, c];
        }

        return new ShipShape(rotatedShape);
    }

    public static ShipShape AvengersHelicarrier => new(new[,]
    {
        { false, true, false, true, false },
        { true, true, true, true, true },
        { false, true, false, true, false }
    });

    public static ShipShape Carrier => new(new[,] { { true, true, true, true, true } });
    public static ShipShape Battleship => new(new[,] { { true, true, true, true } });
    public static ShipShape Destroyer => new(new[,] { { true, true, true } });
    public static ShipShape Submarine => new(new[,] { { true, true, true } });
    public static ShipShape Boat => new(new[,] { { true, true } });
}