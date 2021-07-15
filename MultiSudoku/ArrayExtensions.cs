using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiSudoku
{
    public static class ArrayExtensions
    {
        public static void Fill<T>(this T[] array, T with)
        {
            for (var i = 0; i < array.Length; i++)
                array[i] = with;
        }

        public static T[] ExtendCopy<T>(this T[] array, int index, T value)
        {
            var arr = new T[array.Length];
            Array.Copy(array, arr, arr.Length);
            arr[index] = value;

            return arr;
        }
    }
}
