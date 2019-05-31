using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImageTools.Utilities
{
    public class TypeSize
    {
        private static readonly Dictionary<Type, int> cache = new Dictionary<Type, int>();

        public static int Of(Type t)
        {
            if (cache.ContainsKey(t)) return cache[t];

            var dm = new DynamicMethod("func", typeof(int),
                Type.EmptyTypes, typeof(TypeSize));

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Sizeof, t);
            il.Emit(OpCodes.Ret);

            var func = (Func<int>)dm.CreateDelegate(typeof(Func<int>));
            cache.Add(t, func());

            return cache[t];
        }
    }
}