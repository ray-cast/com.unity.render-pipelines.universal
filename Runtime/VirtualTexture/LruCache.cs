namespace UnityEngine.Rendering.Universal
{
    public class LruCache
    {
        public class NodeInfo
        {
            public int id = 0;
            public NodeInfo next { get; set; }
            public NodeInfo prev { get; set; }
        }

        private NodeInfo [] allNodes;
        private NodeInfo head = null;
        private NodeInfo tail = null;

        public int first { get { return head.id; } }

        public LruCache(int count)
        {
            allNodes = new NodeInfo[count];

            for (int i= 0;i < count;i++)
            {
                allNodes[i] = new NodeInfo()
                {
                    id = i,
                };
            }

            for (int i = 0; i < count; i++)
            {
                allNodes[i].next = (i + 1 < count) ? allNodes[i + 1] : null;
                allNodes[i].prev = (i != 0) ? allNodes[i - 1] : null;
            }

            head = allNodes[0];
            tail = allNodes[count - 1];
        }

        public bool SetActive(int id)
        {
            if (id < 0 || id >= allNodes.Length)
                return false;

            var node = allNodes[id];
            if(node == tail)
            {
                return true;
            }

            Remove(node);
            AddLast(node);
            return true;
        }

        private void AddLast(NodeInfo node)
        {
            var lastTail = tail;
            lastTail.next = node;
            tail = node;
            node.prev = lastTail;
        }

        private void Remove(NodeInfo node)
        {
            if (head == node)
            {
                head = node.next;
            }
            else
            {
                node.prev.next = node.next;
                node.next.prev = node.prev;
            }
        }
    }
}