using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
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

        while (z.Parent is { Color: RbColor.Red } p)
        {
            var gp = p.Parent;
            if (gp == null) break;

            bool parentIsLeft = p.IsLeftChild;
            var uncle = parentIsLeft ? gp.Right : gp.Left;

            // Случай 1: дядя красный — перекраска и подъём
            if (uncle is { Color: RbColor.Red })
            {
                p.Color = RbColor.Black;
                uncle.Color = RbColor.Black;
                gp.Color = RbColor.Red;
                z = gp;
                continue;
            }

            // Случай 2: «ломаная» линия — выравниваем вращением
            if (parentIsLeft != z.IsLeftChild)
            {
                z = p;
                if (parentIsLeft) RotateLeft(z); else RotateRight(z);
                p = z.Parent!;
                gp = p.Parent!;
            }

            // Случай 3: прямая линия — вращение вокруг деда
            p.Color = RbColor.Black;
            gp.Color = RbColor.Red;
            if (parentIsLeft) RotateRight(gp); else RotateLeft(gp);
        }

        if (Root != null)
            Root.Color = RbColor.Black;
    }

    public override bool Remove(TKey key)
    {
        var z = FindNode(key);
        if (z == null) return false;

        var y = z;
        var yOriginalColor = y.Color;
        RbNode<TKey, TValue>? x;
        RbNode<TKey, TValue>? xParent;

        if (z.Left == null || z.Right == null)
        {
            // 0–1 ребёнок: просто подставляем
            x = z.Left ?? z.Right;
            xParent = z.Parent;
            Transplant(z, x);
        }
        else
        {
            // 2 ребёнка: заменяем на преемника (min справа)
            y = z.Right;
            while (y.Left != null)
                y = y.Left;

            yOriginalColor = y.Color;
            x = y.Right;
            xParent = y.Parent == z ? y : y.Parent;

            if (y.Parent != z)
            {
                Transplant(y, y.Right);
                y.Right = z.Right;
                y.Right.Parent = y;
            }

            Transplant(z, y);
            y.Left = z.Left;
            y.Left.Parent = y;
            y.Color = z.Color;
        }

        Count--;

        // Чёрный узел убрали — могли нарушить «чёрную высоту»
        if (yOriginalColor == RbColor.Black)
            DeleteFixup(x, xParent);

        return true;
    }

    private static bool IsBlack(RbNode<TKey, TValue>? node) =>
        node == null || node.Color == RbColor.Black;

    // x — узел (или NIL), который занял место удалённого чёрного; несёт «двойную черноту»
    private void DeleteFixup(RbNode<TKey, TValue>? x, RbNode<TKey, TValue>? xParent)
    {
        while (IsBlack(x) && x != Root)
        {
            if (xParent == null) break;

            bool xIsLeft = x == xParent.Left;
            var w = xIsLeft ? xParent.Right : xParent.Left;
            if (w == null) break;

            // Случай 1: брат красный → делаем его чёрным, родителя красным, вращаем
            if (w.Color == RbColor.Red)
            {
                w.Color = RbColor.Black;
                xParent.Color = RbColor.Red;
                if (xIsLeft) RotateLeft(xParent); else RotateRight(xParent);
                w = xIsLeft ? xParent.Right : xParent.Left;
                if (w == null) break;
            }

            var near = xIsLeft ? w.Left : w.Right; // ребёнок брата ближе к x
            var far = xIsLeft ? w.Right : w.Left;  // ребёнок брата дальше от x

            // Случай 2: оба ребёнка брата чёрные → переносим «дефицит» на родителя
            if (IsBlack(near) && IsBlack(far))
            {
                w.Color = RbColor.Red;
                x = xParent;
                xParent = x.Parent;
                continue;
            }

            // Случай 3: дальний ребёнок чёрный → вращаем брата, сводим к случаю 4
            if (IsBlack(far))
            {
                if (near != null) near.Color = RbColor.Black;
                w.Color = RbColor.Red;
                if (xIsLeft) RotateRight(w); else RotateLeft(w);
                w = (xIsLeft ? xParent.Right : xParent.Left)!;
                far = xIsLeft ? w.Right : w.Left;
            }

            // Случай 4: дальний ребёнок красный → финальное вращение, конец
            w.Color = xParent.Color;
            xParent.Color = RbColor.Black;
            if (far != null) far.Color = RbColor.Black;
            if (xIsLeft) RotateLeft(xParent); else RotateRight(xParent);
            x = Root;
            break;
        }

        if (x != null)
            x.Color = RbColor.Black;
    }
}
