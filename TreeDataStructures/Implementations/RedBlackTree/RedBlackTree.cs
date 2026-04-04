using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        return new RbNode<TKey, TValue>(key, value);
    }
    
    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {

        var z = newNode;

        z.Color = RbColor.Red;

        while (z.Parent is RbNode<TKey, TValue> p && p.Color == RbColor.Red)
        {
            var gp = p.Parent;
            if (gp == null) break;

            if (p == gp.Left)
            {
                var y = gp.Right;
                if (y != null && y.Color == RbColor.Red)
                {
                    p.Color = RbColor.Black;
                    y.Color = RbColor.Black;
                    gp.Color = RbColor.Red;
                    z = gp;
                }
                else
                {
                    if (z == p.Right)
                    {
                        z = p;
                        RotateLeft(z);
                        p = z.Parent;
                        gp = p?.Parent;
                    }

                    p = z.Parent;
                    if (p != null)
                        p.Color = RbColor.Black;
                    if (p?.Parent is RbNode<TKey, TValue> gpp)
                    {
                        gpp.Color = RbColor.Red;
                        RotateRight(gpp);
                    }
                }
            }
            else
            {
                var y = gp.Left;
                if (y != null && y.Color == RbColor.Red)
                {
                    p.Color = RbColor.Black;
                    y.Color = RbColor.Black;
                    gp.Color = RbColor.Red;
                    z = gp;
                }
                else
                {
                    if (z == p.Left)
                    {
                        z = p;
                        RotateRight(z);
                        p = z.Parent;
                        gp = p?.Parent;
                    }

                    p = z.Parent;
                    if (p != null)
                        p.Color = RbColor.Black;
                    if (p?.Parent is RbNode<TKey, TValue> gpp)
                    {
                        gpp.Color = RbColor.Red;
                        RotateLeft(gpp);
                    }
                }
            }
        }

        if (Root is RbNode<TKey, TValue> rootNode)
            rootNode.Color = RbColor.Black;
    }
    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {

    }

    public override bool Remove(TKey key)
    {
        var z = FindNode(key);
        if (z == null) return false;

        RbNode<TKey, TValue>? y = z;
        var yOriginalColor = y.Color;
        RbNode<TKey, TValue>? x;
        RbNode<TKey, TValue>? xParent = null;

        if (z.Left == null)
        {
            x = z.Right;
            xParent = z.Parent;
            Transplant(z, z.Right);
        }
        else if (z.Right == null)
        {
            x = z.Left;
            xParent = z.Parent;
            Transplant(z, z.Left);
        }
        else
        {
            y = z.Right;
            while (y.Left != null)
                y = y.Left;

            yOriginalColor = y.Color;
            x = y.Right;

            if (y.Parent == z)
            {
                xParent = y;
                if (x != null) x.Parent = y;
            }
            else
            {
                xParent = y.Parent;
                Transplant(y, y.Right);
                y.Right = z.Right;
                if (y.Right != null) y.Right.Parent = y;
            }

            Transplant(z, y);
            y.Left = z.Left;
            if (y.Left != null) y.Left.Parent = y;
            y.Color = z.Color;
        }

        Count--;

        if (yOriginalColor == RbColor.Black)
        {
            DeleteFixup(x, xParent);
        }

        return true;
    }

    private void DeleteFixup(RbNode<TKey, TValue>? x, RbNode<TKey, TValue>? xParent)
    {
        while ((x == null || x.Color == RbColor.Black) && x != Root)
        {
            if (xParent == null) break;

            if (x == xParent.Left)
            {
                var w = xParent.Right;
                if (w != null && w.Color == RbColor.Red)
                {
                    w.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateLeft(xParent);
                    w = xParent.Right;
                }

                if ((w?.Left == null || (w!.Left)!.Color == RbColor.Black) &&
                    (w?.Right == null || (w!.Right)!.Color == RbColor.Black))
                {
                    if (w != null) w.Color = RbColor.Red;
                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {
                    if (w?.Right == null || (w!.Right)!.Color == RbColor.Black)
                    {
                        if (w?.Left is RbNode<TKey, TValue> wl) wl.Color = RbColor.Black;
                        if (w != null) w.Color = RbColor.Red;
                        if (w != null) RotateRight(w);
                        w = xParent.Right;
                    }

                    if (w != null) w.Color = xParent.Color;
                    xParent.Color = RbColor.Black;
                    if (w?.Right is RbNode<TKey, TValue> wr) wr.Color = RbColor.Black;
                    RotateLeft(xParent);
                    x = Root;
                    break;
                }
            }
            else
            {
                var w = xParent.Left;
                if (w != null && w.Color == RbColor.Red)
                {
                    w.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateRight(xParent);
                    w = xParent.Left;
                }

                if ((w?.Right == null || w!.Right!.Color == RbColor.Black) &&
                    (w?.Left == null || w!.Left!.Color == RbColor.Black))
                {
                    if (w != null) w.Color = RbColor.Red;
                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {
                    if (w?.Left == null || w!.Left.Color == RbColor.Black)
                    {
                        if (w?.Right is RbNode<TKey, TValue> wr) wr.Color = RbColor.Black;
                        if (w != null) w.Color = RbColor.Red;
                        if (w != null) RotateLeft(w);
                        w = xParent.Left;
                    }

                    if (w != null) w.Color = xParent.Color;
                    xParent.Color = RbColor.Black;
                    if (w?.Left is RbNode<TKey, TValue> wl) wl.Color = RbColor.Black;
                    RotateRight(xParent);
                    x = Root;
                    break;
                }
            }
        }

        if (x != null)
            x.Color = RbColor.Black;
    }
}