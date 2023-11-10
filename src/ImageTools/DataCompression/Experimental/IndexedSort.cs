using System;
using System.Collections.Generic;

namespace ImageTools.DataCompression.Experimental
{
    public class IndexedSort
    {
        /// <summary>
        /// External quick-sort.
        /// </summary>
        /// <param name="length">Size of external array</param>
        /// <param name="compare">Take 2 indexes (i,j). Return 0 if equal, -1 if i is less, 1 if i is greater</param>
        /// <param name="swap">Swap values at two indexes</param>
        public static void ExternalQSort(int length, Func</*idx1*/int,/*idx2*/int, /*sort result*/int> compare, Action<int,int> swap)
        {
            Stack<IdxSpan> stack = new();
            if (length < 2) return;

            stack.Push(new IdxSpan {Left = 0, Right = length - 1});

            while (stack.Count > 0)
            {
                var span = stack.Pop()!;
                var left = span.Left;
                var right = span.Right;
                var activeSpanWidth = span.Right - span.Left;

                if (activeSpanWidth < 2) continue;
                if (activeSpanWidth <= 4){
                    // simple bubble sort for the smallest spans
                    var min = Math.Max(0, left);
                    var max = Math.Min(length-2, right);
                    for (var bi = max; bi >= min; bi--) {
                        for (var b = min; b <= bi; b++) if (compare(b, b+1) > 0) swap(b, b+1);
                    }
                    continue;
                }

                #region pivot
                // pick three sample points - centre and both edges
                var centre = span.Left + activeSpanWidth / 2;
                
                // sort these into relative correct order
                if (compare(span.Left, span.Right) >= 0) { swap(span.Left, span.Right); }
                if (compare(span.Left, centre) >= 0) { swap(span.Left, centre); }
                
                // pick the middle as the pivot
                var pivotPoint = centre;
                #endregion

                // Partition data around pivot *value* by swapping  at left and right sides of a shrinking sample window
                while (true)
                {
                    while (left < right && compare(left, pivotPoint) < 0)
                    {
                        left++;
                    }

                    while (left < right && compare(right, pivotPoint) > 0)
                    {
                        right--;
                    }

                    if (left >= right) { break; }

                    if (compare(left, right) > 0) swap(left, right);
                    left++; right--;
                }

                // recurse
                var adj = (right == span.Left) ? 1 : 0;
                stack.Push(new IdxSpan {Left = right + adj, Right = span.Right}); // right side
                stack.Push(new IdxSpan {Left = span.Left, Right = right}); // left side
            }
        }
        
        internal class IdxSpan
        {
            public int Left { get; set; }
            public int Right { get; set; }
        }
    }
}