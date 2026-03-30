using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        throw new NotImplementedException("Implement Split operation");
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        throw new NotImplementedException("Implement Merge operation");
    }
    

    public override void Add(TKey key, TValue value)
    {
        throw new NotImplementedException("Implement Add using Split and Merge");
    }

    public override bool Remove(TKey key)
    {
        throw new NotImplementedException("Implement Remove using Split and Merge");
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        throw new NotImplementedException();
    }
    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        throw new NotImplementedException();
    }
    
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
        throw new NotImplementedException();
    }
    
}