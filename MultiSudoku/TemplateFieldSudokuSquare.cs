using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiSudoku
{
    public class TemplateFieldSudokuSquare
    {
        public int   Id       { get; set; }
        public Point Position { get; set; }

        public Point GetNextPositionByDirection(int dir)
        {
            switch (dir)
            {
                case 0:
                    return new Point(Position.X + 2, Position.Y - 2);
                case 1:
                    return new Point(Position.X + 2, Position.Y + 2);
                case 2:
                    return new Point(Position.X - 2, Position.Y + 2);
                case 3:
                    return new Point(Position.X - 2, Position.Y - 2);
                default:
                    return new Point();
            }
        }
    }
}
