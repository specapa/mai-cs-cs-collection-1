using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    // Удаление узла с двумя детьми вызывает хук дважды, снизу вверх, пока ссылки ещё не окончательные.
    private AvlNode<TKey, TValue>? _rebalanceFrom;

    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
        => RebalanceUp(newNode.Parent);

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
        => _rebalanceFrom ??= child ?? parent;

    protected override void RemoveNode(AvlNode<TKey, TValue> node)
    {
        _rebalanceFrom = null;
        base.RemoveNode(node);
        RebalanceUp(_rebalanceFrom);
    }

    private static int HeightOf(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static int BalanceOf(AvlNode<TKey, TValue> node)
        => HeightOf(node.Left) - HeightOf(node.Right);

    private static void RecomputeHeight(AvlNode<TKey, TValue>? node)
    {
        if (node == null) return;
        node.Height = Math.Max(HeightOf(node.Left), HeightOf(node.Right)) + 1;
    }

    private void RebalanceUp(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            RecomputeHeight(node);
            int balance = BalanceOf(node);
            node = Math.Abs(balance) > 1 ? Balance(node, balance).Parent : node.Parent;
        }
    }

    private AvlNode<TKey, TValue> Balance(AvlNode<TKey, TValue> node, int balance)
    {
        if (balance > 1)
        {
            if (BalanceOf(node.Left!) < 0)
                RotateBigRight(node);
            else
                RotateRight(node);
        }
        else if (BalanceOf(node.Right!) > 0)
        {
            RotateBigLeft(node);
        }
        else
        {
            RotateLeft(node);
        }

        var root = node.Parent!;
        RecomputeHeight(root.Left);
        RecomputeHeight(root.Right);
        RecomputeHeight(root);
        return root;
    }
}
