using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiSudoku
{
    public static class Utils
    {
        public static class RadixSort
        {
            public static void SudokuDataSortByPoint(List<SudokuData> sudokus, Point pt) => SudokuDataSortByPoint(sudokus, pt.X, pt.Y);

            public static void SudokuDataSortByPoint(List<SudokuData> sudokus, int x, int y)
            {
                var N = 4;

                if (sudokus.Count > 128)
                    N = 8;
                if (sudokus.Count > 65535)
                    N = 16;

                var k      = 32 / N;
                var M      = 1 << N;
                var n      = sudokus.Count;
                var buffer = new SudokuData[n];

                for (var d = 0; d < k; d++)
                {
                    var c = new int[M];

                    for (var i = 0; i < n; i++)
                        c[sudokus[i].Squares[x, y] >> (N * d) & (M - 1)]++;

                    for (var i = 1; i < M; i++)
                        c[i] += c[i - 1];

                    for (var i = n - 1; i >= 0; i--)
                        buffer[--c[sudokus[i].Squares[x, y] >> (N * d) & (M - 1)]] = sudokus[i];

                    for (var i = 0; i < n; i++)
                        sudokus[i] = buffer[i];
                }
            }

            public static void SudokuRadixSortById(List<SudokuData> sudokus)
            {
                var N = 4;

                if (sudokus.Count > 128)
                    N = 8;
                if (sudokus.Count > 65535)
                    N = 16;

                var k      = 32 / N;
                var M      = 1 << N;
                var n      = sudokus.Count;
                var buffer = new SudokuData[n];

                for (var d = 0; d < k; d++)
                {
                    var c = new int[M];

                    for (var i = 0; i < n; i++)
                        c[sudokus[i].Id >> (N * d) & (M - 1)]++;

                    for (var i = 1; i < M; i++)
                        c[i] += c[i - 1];

                    for (var i = n - 1; i >= 0; i--)
                        buffer[--c[sudokus[i].Id >> (N * d) & (M - 1)]] = sudokus[i];

                    for (var i = 0; i < n; i++)
                        sudokus[i] = buffer[i];
                }
            }
            
            public static void ChainRadixSortBy(List<int[]>[] chain, int direction, int x)
            {
                var N = 4;

                if (chain[direction].Count > 128)
                    N = 8;
                if (chain[direction].Count > 65535)
                    N = 16;

                var k      = 32 / N;
                var M      = 1 << N;
                var n      = chain[direction].Count;
                var buffer = new int[n][];

                for (var d = 0; d < k; d++)
                {
                    var c = new int[M];

                    for (var i = 0; i < n; i++)
                        c[chain[direction][i][x] >> (N * d) & (M - 1)]++;

                    for (var i = 1; i < M; i++)
                        c[i] += c[i - 1];

                    for (var i = n - 1; i >= 0; i--)
                        buffer[--c[chain[direction][i][x] >> (N * d) & (M - 1)]] = chain[direction][i];

                    for (var i = 0; i < n; i++)
                        chain[direction][i] = buffer[i];
                }
            }
        }

        public static class BinarySearch
        {
            public static List<int> SearchSudokuDataRange(List<SudokuData> sudokus, SudokuData searchedSudokuData, int fx, int fy, int tx, int ty, bool ignoreTask)
            {
                var list  = new List<int>();
                var index = SearchSudokuData(sudokus, searchedSudokuData.Squares[fx,fy], tx, ty);

                if (index == -1)
                    return list;

                while (index > 0 && sudokus[index - 1].Squares[tx, ty] == searchedSudokuData.Squares[fx, fy]) index--;
                var start = index;

                while (index < sudokus.Count && sudokus[index].Squares[tx, ty] == searchedSudokuData.Squares[fx, fy])
                    index++;

                index--;

                for (var i = start; i <= index; i++)
                {
                    if (ignoreTask)
                    {
                        if (searchedSudokuData.IsEqual(sudokus[i], fx, fy, tx, ty))
                            list.Add(i);
                    }
                    else
                    {
                        if (searchedSudokuData.IsFullEqual(sudokus[i], fx, fy, tx, ty))
                            list.Add(i);
                    }
                }

                return list;
            }

            public static int SearchSudokuData(List<SudokuData> sudokus, int searchedValue, int x, int y)
            {
                return SearchSudokuData(sudokus, searchedValue, x, y, 0, sudokus.Count - 1);
            }

            private static int SearchSudokuData(List<SudokuData> sudokus, int searchedValue, int x, int y, int first, int last)
            {
                if (first > last)
                    return -1;

                var middle      = (first + last) / 2;
                var middleValue = sudokus[middle].Squares[x, y];

                return middleValue == searchedValue ? middle : middleValue > searchedValue ? SearchSudokuData(sudokus, searchedValue, x, y, first, middle - 1) : SearchSudokuData(sudokus, searchedValue, x, y, middle + 1, last);
            }

            public static Tuple<int, int> SearchChainRange(List<int[]>[] chain, int searchedValue, int direction)
            {
                var index = SearchChain(chain, searchedValue, direction);

                if (index == -1)
                    return Tuple.Create(-1, -1);

                while (index > 0 && chain[direction][index - 1][0] == searchedValue) index--;
                var start = index;

                while (index < chain[direction].Count && chain[direction][index][0] == searchedValue)
                    index++;

                return Tuple.Create(start, --index);
            }

            public static int SearchChain(List<int[]>[] chain, int searchedValue, int direction)
            {
                return SearchChain(chain, searchedValue, direction, 0, chain[direction].Count - 1);
            }

            private static int SearchChain(List<int[]>[] chain, int searchedValue, int direction, int first, int last)
            {
                if (first > last)
                    return -1;

                var middle      = (first + last) / 2;
                var middleValue = chain[direction][middle][0];

                return middleValue == searchedValue ? middle : middleValue > searchedValue ? SearchChain(chain, searchedValue, direction, first, middle - 1) : SearchChain(chain, searchedValue, direction, middle + 1, last);
            }
        }

        public static class Directions
        {
            private static readonly Point[] Dir = { new Point(2, 0), new Point(2, 2), new Point(0, 2), new Point(0, 0) };

            public static Point GetDirectionPoint(int dir)
            {
                return Dir[dir];
            }

            public static Point GetInvDirectionPoint(int dir)
            {
                return Dir[(dir + 2) % 4];
            }

            public static Rectangle GetChainRect(int dir)
            {
                return new Rectangle(Dir[dir].X, Dir[dir].Y, Dir[(dir + 2) % 4].X, Dir[(dir + 2) % 4].Y);
            }
        }
    }

}
