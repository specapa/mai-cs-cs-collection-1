using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
{
    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        return new TreapNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        while (newNode.Parent is { } parent && parent.Priority < newNode.Priority)
        {
            if (newNode.IsLeftChild)
                RotateRight(parent);
            else
                RotateLeft(parent);
        }
    }

    protected override void RemoveNode(TreapNode<TKey, TValue> node)
    {
        while (node.Left != null && node.Right != null)
        {
            if (node.Left.Priority > node.Right.Priority)
                RotateRight(node);
            else
                RotateLeft(node);
        }

        Transplant(node, node.Left ?? node.Right);
    }
}
