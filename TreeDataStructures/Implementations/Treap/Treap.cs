using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null) return (null, null);

        int cmp = Comparer.Compare(root.Key, key);
        if (cmp <= 0)
        {
            var (l, r) = Split(root.Right as TreapNode<TKey, TValue>, key);
            root.Right = l;
            if (l != null) l.Parent = root;
            if (r != null) r.Parent = null;
            root.Parent = null;
            return (root, r);
        }
        else
        {
            var (l, r) = Split(root.Left as TreapNode<TKey, TValue>, key);
            root.Left = r;
            if (r != null) r.Parent = root;
            if (l != null) l.Parent = null;
            root.Parent = null;
            return (l, root);
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        if (left.Priority > right.Priority)
        {
            var merged = Merge(left.Right as TreapNode<TKey, TValue>, right);
            left.Right = merged;
            if (merged != null) merged.Parent = left;
            left.Parent = null;
            return left;
        }
        else
        {
            var merged = Merge(left, right.Left as TreapNode<TKey, TValue>);
            right.Left = merged;
            if (merged != null) merged.Parent = right;
            right.Parent = null;
            return right;
        }
    }

    public override void Add(TKey key, TValue value)
    {
        var existing = FindNode(key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        var node = CreateNode(key, value);
        if (Root == null)
        {
            Root = node;
            Count = 1;
            return;
        }

        var current = Root as TreapNode<TKey, TValue>;
        TreapNode<TKey, TValue>? parent = null;
        while (current != null)
        {
            parent = current;
            int cmp = Comparer.Compare(key, current.Key);
            current = cmp < 0 ? current.Left as TreapNode<TKey, TValue> : current.Right as TreapNode<TKey, TValue>;
        }

        node.Parent = parent;
        if (parent == null) Root = node;
        else if (Comparer.Compare(key, parent.Key) < 0) parent.Left = node;
        else parent.Right = node;

        Count++;

        var newNode = node;
        while (newNode.Parent != null && newNode.Parent is TreapNode<TKey, TValue> p && p.Priority < newNode.Priority)
        {
            if (newNode.IsLeftChild)
                RotateRight(p);
            else
                RotateLeft(p);
        }
    }

    public override bool Remove(TKey key)
    {
        var node = FindNode(key) as TreapNode<TKey, TValue>;
        if (node == null) return false;

        while (node.Left != null && node.Right != null)
        {
            var left = node.Left as TreapNode<TKey, TValue>;
            var right = node.Right as TreapNode<TKey, TValue>;
            if (left != null && right != null)
            {
                if (left.Priority > right.Priority)
                    RotateRight(node);
                else
                    RotateLeft(node);
            }
            else if (left != null)
                RotateRight(node);
            else if (right != null)
                RotateLeft(node);
        }

        var parent = node.Parent as TreapNode<TKey, TValue>;
        var child = node.Left ?? node.Right;
        Transplant(node, child as TreapNode<TKey, TValue>);
        OnNodeRemoved(parent, child as TreapNode<TKey, TValue>);
        Count--;
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        return new TreapNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        while (newNode.Parent != null && newNode.Parent is TreapNode<TKey, TValue> p && p.Priority < newNode.Priority)
        {
            if (newNode.IsLeftChild)
                RotateRight(p);
            else
                RotateLeft(p);
        }
    }

    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
    }
}
