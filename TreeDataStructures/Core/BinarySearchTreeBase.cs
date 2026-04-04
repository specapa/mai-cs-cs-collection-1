using System.Collections;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        if (Root == null)
        {
            TNode node = CreateNode(key, value);
            Root = node;
            Count = 1;
            return;
        }

        TNode? current = Root, prev = null;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0)
            {
                current.Value = value;
                return;
            }
            prev = current;
            current = cmp < 0 ? current.Left : current.Right;
        }

        TNode newNode = CreateNode(key, value);
        newNode.Parent = prev;
        if (Comparer.Compare(key, prev!.Key) < 0)
            prev.Left = newNode;
        else
            prev.Right = newNode;

        Count++;

        OnNodeAdded(newNode);
    }

    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }


    protected virtual void RemoveNode(TNode node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));

        if (node.Left == null)
        {
            var parent = node.Parent;
            Transplant(node, node.Right);
            OnNodeRemoved(parent, node.Right);
        }
        else if (node.Right == null)
        {
            var parent = node.Parent;
            Transplant(node, node.Left);
            OnNodeRemoved(parent, node.Left);
        }
        else
        {
            var smallestRight = node.Right;
            while (smallestRight.Left != null)
            {
                smallestRight = smallestRight.Left;
            }

            if (smallestRight.Parent != node)
            {
                var srParent = smallestRight.Parent;
                Transplant(smallestRight, smallestRight.Right);
                OnNodeRemoved(srParent, smallestRight.Right);

                smallestRight.Right = node.Right;
                if (smallestRight.Right != null)
                    smallestRight.Right.Parent = smallestRight;
            }

            var parent = node.Parent;
            Transplant(node, smallestRight);
            OnNodeRemoved(parent, smallestRight);

            smallestRight.Left = node.Left;
            if (smallestRight.Left != null)
                smallestRight.Left.Parent = smallestRight;
        }
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected void RotateLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y == null) { return; }

        x.Right = y.Left;
        y.Left?.Parent = x;
        y.Parent = x.Parent;
        if (x.Parent == null)
        {
            Root = y;
        }
        else if (x.IsLeftChild)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }
        y.Left = x;
        x.Parent = y;
    }

    protected void RotateRight(TNode y)
    {
        TNode? x = y.Left;
        if (x == null) { return; }

        y.Left = x.Right;
        x.Right?.Parent = y;
        x.Parent = y.Parent;
        if (y.Parent == null)
        {
            Root = x;
        }
        else if (y.IsLeftChild)
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }
        x.Right = y;
        y.Parent = x;
    }

    protected void RotateBigLeft(TNode x)
    {
        Debug.Assert(x.Right != null);
        RotateRight(x.Right!);
        RotateLeft(x);
    }

    protected void RotateBigRight(TNode y)
    {
        Debug.Assert(y.Left != null);
        RotateLeft(y.Left!);
        RotateRight(y);
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        RotateBigLeft(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        RotateBigRight(y);
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }
    #endregion
    

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() =>
    new TreeIterator(Root, TraversalStrategy.InOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() =>
        new TreeIterator(Root, TraversalStrategy.PreOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() =>
        new TreeIterator(Root, TraversalStrategy.PostOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() =>
        new TreeIterator(Root, TraversalStrategy.InOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() =>
        new TreeIterator(Root, TraversalStrategy.PreOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() =>
        new TreeIterator(Root, TraversalStrategy.PostOrderReverse);

    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private class TreeIterator :
        IEnumerable<TreeEntry<TKey, TValue>>,
        IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly TraversalStrategy _strategy;

        private TNode? _current;
        private bool _started;

        public TreeIterator(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            _strategy = strategy;
            _current = null;
            _started = false;
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => new TreeIterator(_root, _strategy);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TreeEntry<TKey, TValue> Current =>
            new TreeEntry<TKey, TValue>(_current!.Key, _current.Value, _current.Height);

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return _strategy switch
            {
                TraversalStrategy.InOrder => MoveNextInOrder(),
                TraversalStrategy.PreOrder => MoveNextPreOrder(),
                TraversalStrategy.PostOrder => MoveNextPostOrder(),
                TraversalStrategy.InOrderReverse => MoveNextInOrderReverse(),
                TraversalStrategy.PreOrderReverse => MoveNextPreOrderReverse(),
                TraversalStrategy.PostOrderReverse => MoveNextPostOrderReverse(),
                _ => false
            };
        }

        public void Reset()
        {
            _current = null;
            _started = false;
        }

        public void Dispose() { }


        // InOrder


        private bool MoveNextInOrder()
        {
            if (!_started)
            {
                _current = LeftMost(_root);
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = NextInOrder(_current);
            return _current != null;
        }

        private static TNode? NextInOrder(TNode node)
        {
            if (node.Right != null)
                return LeftMost(node.Right);

            while (node.Parent != null && node == node.Parent.Right)
                node = node.Parent;

            return node.Parent;
        }

        
        // Reverse InOrder
        

        private bool MoveNextInOrderReverse()
        {
            if (!_started)
            {
                _current = RightMost(_root);
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = PrevInOrder(_current);
            return _current != null;
        }

        private static TNode? PrevInOrder(TNode node)
        {
            if (node.Left != null)
                return RightMost(node.Left);

            while (node.Parent != null && node == node.Parent.Left)
                node = node.Parent;

            return node.Parent;
        }

        
        // PreOrder
        

        private bool MoveNextPreOrder()
        {
            if (!_started)
            {
                _current = _root;
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = NextPreOrder(_current);
            return _current != null;
        }

        private static TNode? NextPreOrder(TNode node)
        {
            if (node.Left != null)
                return node.Left;

            if (node.Right != null)
                return node.Right;

            while (node.Parent != null)
            {
                if (node == node.Parent.Left && node.Parent.Right != null)
                    return node.Parent.Right;

                node = node.Parent;
            }

            return null;
        }

        
        // Reverse PreOrder (Root → Right → Left)
        

        private bool MoveNextPreOrderReverse()
        {
            if (!_started)
            {
                _current = LastPreOrder(_root);
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = PrevPreOrder(_current);
            return _current != null;
        }

        private static TNode? LastPreOrder(TNode? node)
        {
            if (node == null) return null;

            while (true)
            {
                if (node.Right != null)
                    node = node.Right;
                else if (node.Left != null)
                    node = node.Left;
                else
                    return node;
            }
        }

        private static TNode? PrevPreOrder(TNode node)
        {
            if (node.Parent == null) return null;

            var parent = node.Parent;
            if (node == parent.Left)
            {
                return parent;
            }

            if (parent.Left != null)
                return LastPreOrder(parent.Left);

            return parent;
        }

        
        // PostOrder
        

        private bool MoveNextPostOrder()
        {
            if (!_started)
            {
                _current = FirstPostOrder(_root);
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = NextPostOrder(_current);
            return _current != null;
        }

        private static TNode? FirstPostOrder(TNode? node)
        {
            if (node == null) return null;

            while (true)
            {
                if (node.Left != null)
                    node = node.Left;
                else if (node.Right != null)
                    node = node.Right;
                else
                    return node;
            }
        }

        private static TNode? NextPostOrder(TNode node)
        {
            if (node.Parent == null)
                return null;

            if (node == node.Parent.Left && node.Parent.Right != null)
                return FirstPostOrder(node.Parent.Right);

            return node.Parent;
        }

        
        // Reverse PostOrder (Root last → reverse)
        

        private bool MoveNextPostOrderReverse()
        {
            if (!_started)
            {
                _current = _root;
                _started = true;
                return _current != null;
            }

            if (_current == null) return false;

            _current = PrevPostOrder(_current);
            return _current != null;
        }

        private static TNode? PrevPostOrder(TNode node)
        {
            if (node.Right != null)
                return node.Right;

            if (node.Left != null)
                return node.Left;

            while (node.Parent != null)
            {
                if (node == node.Parent.Right && node.Parent.Left != null)
                    return node.Parent.Left;

                node = node.Parent;
            }

            return null;
        }

        
        // Helpers
        

        private static TNode? LeftMost(TNode? node)
        {
            while (node?.Left != null)
                node = node.Left;
            return node;
        }

        private static TNode? RightMost(TNode? node)
        {
            while (node?.Right != null)
                node = node.Right;
            return node;
        }
    }


    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var e in InOrder())
            yield return new KeyValuePair<TKey, TValue>(e.Key, e.Value);
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        int i = arrayIndex;
        foreach (var e in InOrder())
        {
            if (i >= array.Length) throw new ArgumentException("The destination array is too small.", nameof(array));
            array[i++] = new KeyValuePair<TKey, TValue>(e.Key, e.Value);
        }
    }
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}