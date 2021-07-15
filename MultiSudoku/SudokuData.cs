using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiSudoku
{
    public class SudokuData
    {
        public int Id { get; set; }
        public int[,] Squares { get; set; }
        public short[,] Tasks { get; set; }
        public bool Used { get; set; }

        public SudokuData(int id, string str)
        {
            Id = id;
            Squares = new int[3, 3];
            Tasks = new short[3, 3];
            var strArr = str.Split(' ');
            var tasks = strArr[0];
            var nums = strArr[1];

            for (var i = 0; i < 9; i++)
            {
                Squares[i % 3, i / 3] = Convert.ToInt32(nums.Substring(i % 3 * 3 + i / 3 * 27, 3) + nums.Substring(i % 3 * 3 + 9 + i / 3 * 27, 3) + nums.Substring(i % 3 * 3 + 18 + i / 3 * 27, 3));
                var taskstr = tasks.Substring(i % 3 * 3 + i / 3 * 27, 3) + tasks.Substring(i % 3 * 3 + 9 + i / 3 * 27, 3) + tasks.Substring(i % 3 * 3 + 18 + i / 3 * 27, 3);
                var ba = new BitArray(9);

                for (var j = 0; j < 9; j++)
                    ba.Set(j, taskstr[j] != '.');

                var res = new int[1];
                ba.CopyTo(res, 0);
                Tasks[i % 3, i / 3] = (short)res[0];
            }
        }

        public bool IsEqual(SudokuData sudoku, int fx, int fy, int tx, int ty)
        {
            return Squares[fx, fy] == sudoku.Squares[tx, ty];// && Tasks[fx, fy] == sudoku.Tasks[tx, ty];
        }

        public bool IsFullEqual(SudokuData sudoku, int fx, int fy, int tx, int ty)
        {
            return Squares[fx, fy] == sudoku.Squares[tx, ty] && Tasks[fx, fy] == sudoku.Tasks[tx, ty];
        }

        public override string ToString()
        {
            var nums = "";
            var tasks = "";
            for (var i = 0; i < 9; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    nums += GetDigitBlock(Squares[j, i / 3], i % 3);

                    for (var k = 0; k < 3; k++)
                    {
                        var ba = new BitArray(new[] { (int)Tasks[j, i / 3] });
                        tasks += ba[i % 3 * 3 + k] ? GetDigit(Squares[j, i / 3], i % 3 * 3 + k).ToString() : ".";
                    }
                }
            }
            return $"{tasks} {nums}";
        }

        private int GetDigit(int num, int index)
        {
            return num / (int)Math.Pow(10, 9 - index - 1) % 10;
        }

        private string GetDigitBlock(int num, int index)
        {
            var str = "";
            str += GetDigit(num, index * 3);
            str += GetDigit(num, index * 3 + 1);
            str += GetDigit(num, index * 3 + 2);
            return str;
        }
    }
}
