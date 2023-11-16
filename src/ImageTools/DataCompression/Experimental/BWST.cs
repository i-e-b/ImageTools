using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ImageTools.DataCompression.Experimental
{
    /// <summary>
    /// https://github.com/zephyrtronium/bwst
    /// </summary>
    public class Bwst
    {
        /// <summary>
        /// Compute the Burrows-Wheeler-Scott transform of 's'. This is done out-of-place.
        /// </summary>
        public static byte[] ForwardTransform(byte[] s) {
            var words = Factorize(s); // this is producing a different result from the go sources
            
            // Sorting all rotations of all Lyndon words and then choosing the last
            // character of each is the same as choosing the character to the left of
            // each character in its Lyndon word in sorted order. Therefore, we find
            // all locations of each character, sort them all by their rotations, and
            // proceed therein.
            var locs = Locate(s, words);
            var b = new List<byte>();
            
            Console.WriteLine();

            for (var i = 0; i < locs.Length; i++)
            {
                var charLocs = locs[i];
                if (charLocs is null) continue;

                SortRotations(s, words, charLocs);
            }

            foreach (var charLocs in locs) {
                if (charLocs is null) continue;
                
                foreach (var l in charLocs) {
                    var word = Slice(s, words[l.Word], words[l.Word+1]); //word := s[words[l.word]:words[l.word+1]] // inclusive start index : exclusive end index
                    var i = l.Idx - 1;
                    if (i < 0) {
                        i = word.Length - 1;
                    }
                    b.Add(word[i]);
                }
            }
            return b.ToArray();
        }

        /// <summary>
        /// Compute the inverse of the Burrows-Wheeler-Scott transform of 's'. This is done out-of-place.
        /// </summary>
        public static byte[] ReverseTransform(byte[] b)
        {
            var sorted = b.ToList(); // make a copy
            sorted.Sort();
            
            var used = new bool[b.Length+1]; // big array of false. In the Go code, this is a BigInt -- which we might want to do here
            //used[b.Length] = true;
            var links = new int[b.Length];
            
            // TODO: use binary search in sorted instead of linear search in b
            for (int i = 0; i < sorted.Count; i++){//for i, c := range sorted {
                var c = sorted[i];
                // find the first unused index in b of c
                for (int j=0; j<b.Length; j++){//for j, c2 := range b {
                    var c2 = b[j];
                    if (c == c2 && !used[j]) {
                        links[i] = j;
                        used[j] = true;//used.SetBit(used, j, 1)
                        break;
                    }
                }
            }
            int x;
            // We need to know once again whether each byte is used, so instead of
            // resetting the bitset or using more memory, we can just ask whether it's
            // unused.
            var unused = used;
            var words = new List<byte[]>();//words := multibytesorter{}
            for (int i = 0; i < sorted.Count; i++) { //for i := range sorted {
                if (!unused[i]) continue;
                
                var word = new List<byte>();
                x = i;
                while (unused[x]) {//for unused.Bit(x) == 1 {
                    word.Add(sorted[x]);//word = append(word, sorted[x])
                    unused[x] = false; //unused.SetBit(unused, x, 0)
                    x = links[x];
                }
                /*words = append(words, nil) // add null at end of list
                    copy(words[1:], words) // <- copy everything down one position (overwriting the null)
                    words[0] = word // put new set at front */
                words.Insert(0, word.ToArray());
            }
            //if !sort.IsSorted(words) {
            //    sort.Sort(words)
            //}
            words.Sort(LexicographicByteSort);
            x = b.Length;//x := len(b)
            
            var s = new byte[b.Length];
            foreach (var word in words)
            {
                x -= word.Length;
                CopyOver(s, x, word);
            }
            return s;

            /*s := make([]byte, len(b))
            for _, word := range words {
                x -= len(word)
                copy(s[x:], word)
            }
            return s;*/
        }

        private static void CopyOver(byte[] bytes, int offset, byte[] word)
        {
            foreach (var v in word)
            {
                if (offset >= bytes.Length) return;
                bytes[offset++] = v;
            }
        }

        private static int LexicographicByteSort(byte[] x, byte[] y)
        {
            var min = Math.Min(x.Length, y.Length);
            var i = 0;
            for (; i < min; i++)
            {
                if (x[i] < y[i]) return -1;
                if (x[i] > y[i]) return 1;
            }
            // same up to this point
            if (x.Length < y.Length) return -1;
            if (x.Length > y.Length) return 1;
            return 0; // identical
        }

        // Compute the Lyndon factorization of s. Includes both endpoints.
        private static int[] Factorize(byte[] s) {
            // Do an initial pass to count the number of words. Hopefully this avoids
            // enough copying to be faster.
            return FindLyndon(s).ToArray();
        }
        
        // Duval's algorithm
        private static List<int> FindLyndon(byte[] s) {
            var result = new List<int> { 0 };
            // Thanks to Jonathan on golang-nuts for simplifying the inner loop.
            var k = -1;
            while (k < (s.Length -1)) {
                var i = k+1;
                var j = k+2;
                while ((j < s.Length) && (s[i] <= s[j])) {
                    if (s[i] < s[j]) {
                        // Whenever a character is less than the first character of a
                        // Lyndon word, it is not in that word.
                        i = k;
                    }
                    // When the character at i is equal to the character at the start
                    // of the word, whether it is a part of that word or the start of
                    // the next is determined by the remainder of the string: if the
                    // substring s[k..n] < s[i..n], then s[i] is in the word starting
                    // at k.
                    i++;
                    j++;
                }
                while (k < i) {
                    k += j - i;
                    result.Add(k+1);
                }
            }
            return result;
        }
        
        // Each instance of a character is considered to be at the beginning of a
        // rotation of its word, so the locations can be sorted. Because each char is
        // in order already, we only need to sort the occurrences of each char
        // separately to sort the entire thing.
        private static void SortRotations(byte[] s, int[] words, LocList locs) {
            locs.Sort((i, j) => {// Cyclic order - AXYA < AXY here because AXYAAXYA < AXYAXY
                var loc1 = i;//locs[i];
                var loc2 = j;//locs[j];
                // get the actual sequences
                var w1 = Slice(s,words[loc1.Word],words[loc1.Word + 1]);
                var w2 = Slice(s,words[loc2.Word],words[loc2.Word + 1]);
                var x = loc1.Idx;
                var y = loc2.Idx;
                var n = lcm(w1.Length, w2.Length);
                for (var count = 0; count < n; count++) {
                    var a = (int)w1[x];
                    var b = (int)w2[y];
                    if (a < b) { return -1; }
                    if (a > b) { return 1; }
                        
                    x++;
                    if (x >= w1.Length) { x = 0; }
                        
                    y++;
                    if (y >= w2.Length) { y = 0; }
                }
                // words are equal
                return 0;});
        }

        private static int gcd(int m, int n ) {
            while (m != 0) {
                var tmp = m;
                m = n % m;
                n = tmp;
            }
            return n;
        }

        private static int lcm(int m, int n) {
            return m / gcd(m, n) * n;
        }

        private static byte[] Slice(byte[] src, int incStartIdx, int exclEndIdx)
        {
            var length = exclEndIdx - incStartIdx;
            return length < 1 ? Array.Empty<byte>() : src.Skip(incStartIdx).Take(length).ToArray();
        }

        private static LocList?[] Locate(byte[] s, int[] words) {
            var locs = new LocList?[256];
            var w = 0;
            
            for (int i = 0; i < s.Length; i++){
                var c = s[i];
                if (i >= words[w+1]) {
                    w++;
                }
                locs[c] = LocList.Append(locs[c], new Loc{Word = w, Idx = i - words[w]});
            }
            return locs;
        }

        private class LocList : IEnumerable<Loc>
        {
            private readonly List<Loc> _list = new();
            public int Length => _list.Count;

            public static LocList Append(LocList? existing, Loc item)
            {
                var list = existing ?? new LocList();
                list._list.Add(item);
                return list;
            }

            public IEnumerator<Loc> GetEnumerator() => _list.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

            public Loc this[int i]
            {
                get => _list[i];
                set => _list[i] = value;
            }

            public void Sort(Comparison<Loc> comparison)
            {
                _list.Sort(comparison);
            }
        }

        private struct Loc
        {
            public int Word;
            public int Idx;
        }
    }
}