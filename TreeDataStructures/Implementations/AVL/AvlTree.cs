using TreeDataStructures.Core;
using System;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        var node = newNode.Parent as AvlNode<TKey, TValue>;
        RebalanceUp(node);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        RebalanceUp(parent as AvlNode<TKey, TValue>);
    }

    private static int HeightOf(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static void RecomputeHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = Math.Max(HeightOf(node.Left as AvlNode<TKey, TValue>), HeightOf(node.Right as AvlNode<TKey, TValue>)) + 1;
    }

    private void RebalanceUp(AvlNode<TKey, TValue>? start)
    {
        var node = start;
        while (node != null)
        {
            RecomputeHeight(node);
            int balance = HeightOf(node.Left as AvlNode<TKey, TValue>) - HeightOf(node.Right as AvlNode<TKey, TValue>);

            if (balance > 1)
            {
                var left = node.Left as AvlNode<TKey, TValue>;
                if (left != null && HeightOf(left.Left as AvlNode<TKey, TValue>) >= HeightOf(left.Right as AvlNode<TKey, TValue>))
                {
                    // Right rotation
                    RotateRight(node);
                    // update heights
                    var parent = node.Parent as AvlNode<TKey, TValue>;
                    RecomputeHeight(node);
                    if (parent != null) RecomputeHeight(parent);
                    node = parent;
                    continue;
                }
                else
                {
                    // Left-Right
                    if (left != null)
                        RotateLeft(left);
                    RotateRight(node);
                    var parent = node.Parent as AvlNode<TKey, TValue>;
                    RecomputeHeight(node);
                    if (parent != null) RecomputeHeight(parent);
                    node = parent;
                    continue;
                }
            }
            else if (balance < -1)
            {
                var right = node.Right as AvlNode<TKey, TValue>;
                if (right != null && HeightOf(right.Right as AvlNode<TKey, TValue>) >= HeightOf(right.Left as AvlNode<TKey, TValue>))
                {
                    // Left rotation
                    RotateLeft(node);
                    var parent = node.Parent as AvlNode<TKey, TValue>;
                    RecomputeHeight(node);
                    if (parent != null) RecomputeHeight(parent);
                    node = parent;
                    continue;
                }
                else
                {
                    // Right-Left
                    if (right != null)
                        RotateRight(right);
                    RotateLeft(node);
                    var parent = node.Parent as AvlNode<TKey, TValue>;
                    RecomputeHeight(node);
                    if (parent != null) RecomputeHeight(parent);
                    node = parent;
                    continue;
                }
            }

            node = node.Parent as AvlNode<TKey, TValue>;
        }
    }

    
}