using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        // Splay the newly added node to the root
        Splay(newNode);
    }
    
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        // After removal, splay the parent if present, otherwise splay the child
        if (parent != null) Splay(parent);
        else if (child != null) Splay(child);
    }
    
    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var node = typeof(BinarySearchTree<TKey, TValue>)
            .GetMethod("FindNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(this, new object[] { key }) as BstNode<TKey, TValue>;

        if (node != null)
        {
            Splay(node);
            value = node.Value;
            return true;
        }

        value = default;
        return false;
    }

    public override bool ContainsKey(TKey key)
    {
        var node = typeof(BinarySearchTree<TKey, TValue>)
            .GetMethod("FindNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(this, new object[] { key }) as BstNode<TKey, TValue>;

        if (node != null)
        {
            Splay(node);
            return true;
        }
        return false;
    }

    private void Splay(BstNode<TKey, TValue>? x)
    {
        while (x != null && x.Parent != null)
        {
            var p = x.Parent;
            var g = p.Parent;

            if (g == null)
            {
                // Zig
                if (x.IsLeftChild)
                    RotateRight(p);
                else
                    RotateLeft(p);
            }
            else if (x.IsLeftChild && p.IsLeftChild)
            {
                // Zig-Zig
                RotateRight(g!);
                RotateRight(p);
            }
            else if (x.IsRightChild && p.IsRightChild)
            {
                // Zig-Zig
                RotateLeft(g!);
                RotateLeft(p);
            }
            else if (x.IsRightChild && p.IsLeftChild)
            {
                // Zig-Zag
                RotateLeft(p);
                RotateRight(g!);
            }
            else if (x.IsLeftChild && p.IsRightChild)
            {
                // Zig-Zag
                RotateRight(p);
                RotateLeft(g!);
            }
        }
    }
    
}
