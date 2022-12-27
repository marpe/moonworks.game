namespace MyGame.Fonts;

public class RectPacker
{
    public struct RectNode
    {
        public int X;
        public int Y;
        public int Width;
    }
    
    public int Height;
    public int Width;
    public RectNode[] Nodes;
    public int NumberOfNodes;

    public RectPacker(int width, int height)
    {
        Width = width;
        Height = height;

        Nodes = new RectNode[256];
        Nodes[0].X = 0;
        Nodes[0].Y = 0;
        Nodes[0].Width = Width;
        NumberOfNodes++;
    }
    
    public void InsertNode(int nodeId, int x, int y, int width)
    {
        if (NumberOfNodes + 1 > Nodes.Length)
        {
            var oldNodes = Nodes;
            var newLength = Nodes.Length == 0 ? 8 : Nodes.Length * 2;
            Nodes = new RectNode[newLength];
            for (var i = 0; i < oldNodes.Length; ++i)
            {
                Nodes[i] = oldNodes[i];
            }
        }

        for (var i = NumberOfNodes; i > nodeId; i--)
        {
            Nodes[i] = Nodes[i - 1];
        }

        Nodes[nodeId].X = x;
        Nodes[nodeId].Y = y;
        Nodes[nodeId].Width = width;
        NumberOfNodes++;
    }

    public void RemoveNode(int nodeIdx)
    {
        if (NumberOfNodes == 0)
            return;

        for (var i = nodeIdx; i < NumberOfNodes - 1; i++)
        {
            Nodes[i] = Nodes[i + 1];
        }

        NumberOfNodes--;
    }

    public void Reset(int width, int height)
    {
        Width = width;
        Height = height;
        NumberOfNodes = 0;
        Nodes[0].X = 0;
        Nodes[0].Y = 0;
        Nodes[0].Width = width;
        NumberOfNodes++;
    }

    public bool AddSkylineLevel(int nodeIdx, int x, int y, int width, int height)
    {
        InsertNode(nodeIdx, x, y + height, width);
        
        for (var i = nodeIdx + 1; i < NumberOfNodes; i++)
        {
            if (Nodes[i].X < Nodes[i - 1].X + Nodes[i - 1].Width)
            {
                var shrink = Nodes[i - 1].X + Nodes[i - 1].Width - Nodes[i].X;
                Nodes[i].X += shrink;
                Nodes[i].Width -= shrink;
                if (Nodes[i].Width <= 0)
                {
                    RemoveNode(i);
                    i--;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        for (var i = 0; i < NumberOfNodes - 1; i++)
        {
            if (Nodes[i].Y != Nodes[i + 1].Y)
                continue;

            Nodes[i].Width += Nodes[i + 1].Width;
            RemoveNode(i + 1);
            i--;
        }

        return true;
    }

    public int RectFits(int nodeIdx, int width, int height)
    {
        var x = Nodes[nodeIdx].X;
        var y = Nodes[nodeIdx].Y;
        if (x + width > Width)
            return -1;

        var spaceLeft = width;
        while (spaceLeft > 0)
        {
            if (nodeIdx == NumberOfNodes)
                return -1;

            y = Math.Max(y, Nodes[nodeIdx].Y);
            if (y + height > Height)
                return -1;

            spaceLeft -= Nodes[nodeIdx].Width;
            ++nodeIdx;
        }

        return y;
    }

    public bool AddRect(int width, int height, ref int dstX, ref int dstY)
    {
        var bestH = Height;
        var bestW = Width;
        var bestI = -1;
        var bestX = -1;
        var bestY = -1;
        
        for (var i = 0; i < NumberOfNodes; i++)
        {
            var y = RectFits(i, width, height);
            if (y == -1)
                continue;

            if (y + height >= bestH && (y + height != bestH || Nodes[i].Width >= bestW))
                continue;

            bestI = i;
            bestW = Nodes[i].Width;
            bestH = y + height;
            bestX = Nodes[i].X;
            bestY = y;
        }

        if (bestI == -1)
            return false;

        if (!AddSkylineLevel(bestI, bestX, bestY, width, height))
            return false;

        dstX = bestX;
        dstY = bestY;
        return true;
    }
}
