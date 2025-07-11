using System.Collections;

namespace ImageTools.DataCompression.Huffman
{
    public class ByteHuffmanTree
    {
        private readonly List<Node> _nodes = new();
        public Node? Root { get; set; }
        public readonly Dictionary<byte, int> Frequencies = new();

        public void Build(byte[] source)
        {
            foreach (var b in source)
            {
                if (!Frequencies.ContainsKey(b))
                {
                    Frequencies.Add(b, 0);
                }

                Frequencies[b]++;
            }

            foreach (var symbol in Frequencies)
            {
                _nodes.Add(new Node { Symbol = symbol.Key, Frequency = symbol.Value });
            }

            while (_nodes.Count > 1)
            {
                var orderedNodes = _nodes.OrderBy(node => node.Frequency).ToList();

                if (orderedNodes.Count >= 2)
                {
                    // Take first two items
                    var taken = orderedNodes.Take(2).ToList();

                    // Create a parent node by combining the frequencies
                    var parent = new Node
                    {
                        Symbol = 0x7F,
                        Frequency = taken[0]!.Frequency + taken[1]!.Frequency,
                        Left = taken[0]!,
                        Right = taken[1]!
                    };

                    _nodes.Remove(taken[0]);
                    _nodes.Remove(taken[1]);
                    _nodes.Add(parent);
                }

                Root = _nodes.FirstOrDefault();
            }
        }

        public BitArray Encode(byte[] source)
        {
            var encodedSource = new List<bool>();

            if (Root is null) return new BitArray(encodedSource.ToArray());
            
            foreach (var c in source)
            {
                var encodedSymbol = Root.Traverse(c, new List<bool>());
                if (encodedSymbol is not null) encodedSource.AddRange(encodedSymbol);
            }

            return new BitArray(encodedSource.ToArray());
        }

        public byte[] Decode(BitArray bits)
        {
            var decoded = new List<byte>();
            if (Root is null) return decoded.ToArray();
            var current = Root;

            foreach (bool bit in bits)
            {
                if (bit)
                {
                    if (current.Right != null)
                    {
                        current = current.Right;
                    }
                }
                else
                {
                    if (current.Left != null)
                    {
                        current = current.Left;
                    }
                }

                if (IsLeaf(current))
                {
                    decoded.Add(current.Symbol);
                    current = Root;
                }
            }

            return decoded.ToArray();
        }

        public static bool IsLeaf(Node node)
        {
            return node.Left is null && node.Right is null;
        }
        
        public class Node
        {
            public byte Symbol { get; set; }
            public int Frequency { get; set; }
            public Node? Right { get; set; }
            public Node? Left { get; set; }

            public List<bool>? Traverse(byte symbol, List<bool> data)
            {
                // Leaf
                if (Right == null && Left == null)
                {
                    if (symbol == Symbol)
                    {
                        return data;
                    }

                    return null;
                }

                List<bool>? left = null;
                List<bool>? right = null;

                if (Left != null)
                {
                    var leftPath = new List<bool>();
                    leftPath.AddRange(data);
                    leftPath.Add(false);

                    left = Left.Traverse(symbol, leftPath);
                }

                if (Right != null)
                {
                    var rightPath = new List<bool>();
                    rightPath.AddRange(data);
                    rightPath.Add(true);
                    right = Right.Traverse(symbol, rightPath);
                }

                if (left != null)
                {
                    return left;
                }

                return right;
            }
        }
    }
}