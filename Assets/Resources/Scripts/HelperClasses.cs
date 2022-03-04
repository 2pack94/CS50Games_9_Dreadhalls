using System;
using System.Collections.Generic;

namespace HelperClasses
{

// Get all combinations (without repetition) of the elements in the input array.
// Each combination consists of m elements from the input array.
// Example for array = [1, 2, 3, 4] and m = 2:
//  [1, 2], [1, 3], [1, 4], [2, 3], [2, 4], [3, 4]
// code copied from: https://codereview.stackexchange.com/questions/194967/get-all-combinations-of-selecting-k-elements-from-an-n-sized-array/195025#195025
static class Combinations
{
    // Enumerate all possible m-size combinations of [0, 1, ..., n-1] array
    // in lexicographic order (first [0, 1, 2, ..., m-1]).
    private static IEnumerable<int[]> CombinationsWoRecursion(int m, int n)
    {
        int[] result = new int[m];
        Stack<int> stack = new Stack<int>(m);
        stack.Push(0);
        while (stack.Count > 0)
        {
            int index = stack.Count - 1;
            int value = stack.Pop();
            while (value < n)
            {
                result[index++] = value++;
                stack.Push(value);
                if (index != m) continue;
                yield return (int[])result.Clone(); // thanks to @xanatos
                //yield return result;
                break;
            }
        }
    }

    public static IEnumerable<T[]> CombinationsWoRecursion<T>(T[] array, int m)
    {
        if (array.Length < m)
            throw new ArgumentException("Array length can't be less than number of selected elements");
        if (m < 1)
            throw new ArgumentException("Number of selected elements can't be less than 1");
        T[] result = new T[m];
        foreach (int[] j in CombinationsWoRecursion(m, array.Length))
        {
            for (int i = 0; i < m; i++)
            {
                result[i] = array[j[i]];
            }
            yield return result;
        }
    }
}

} // namespace HelperClasses
