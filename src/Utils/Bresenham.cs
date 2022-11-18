namespace MyGame.Utils;

public static class Bresenham
{
    /// <summary>
    /// https://github.com/deepnight/deepnightLibs/blob/master/src/dn/Bresenham.hx
    /// </summary>
    public static IEnumerable<(int x, int y)> Line(int x, int y, int x2, int y2)
    {
        var w = x2 - x;
        var h = y2 - y;
        
        int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
        
        if (w < 0)
            dx1 = -1;
        else if (w > 0)
            dx1 = 1;
        if (h < 0)
            dy1 = -1;
        else if (h > 0)
            dy1 = 1;
        if (w < 0)
            dx2 = -1;
        else if (w > 0)
            dx2 = 1;
        
        var longest = Math.Abs(w);
        var shortest = Math.Abs(h);
        
        if (!(longest > shortest))
        {
            longest = Math.Abs(h);
            shortest = Math.Abs(w);
            if (h < 0)
                dy2 = -1;
            else if (h > 0)
                dy2 = 1;
            dx2 = 0;
        }

        var numerator = longest >> 1;
        for (var i = 0; i <= longest; i++)
        {
            yield return (x, y);
            numerator += shortest;
            if (!(numerator < longest))
            {
                numerator -= longest;
                x += dx1;
                y += dy1;
            }
            else
            {
                x += dx2;
                y += dy2;
            }
        }
    }
}
