using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public interface ITreeNode<TNode>
        where TNode : ITreeNode<TNode>
    {
        TNode Parent { get; set; }
        IReadOnlyList<TNode> Children { get; }
    }

    public abstract class TreeNode<TNode> : ITreeNode<TNode>
        where TNode : TreeNode<TNode>
    {
        private TNode _parent;
        private List<TNode> _children = new List<TNode>();

        public virtual TNode Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    if (_parent != null)
                    {
                        lock (_parent._children)
                        {
                            _parent._children.Remove((TNode)this);
                        }
                    }

                    _parent = value;

                    if (_parent != null)
                    {
                        lock (_parent._children)
                        {
                            _parent._children.Add((TNode)this);
                        }
                    }
                }
            }
        }

        public IReadOnlyList<TNode> Children => _children;
    }
}
