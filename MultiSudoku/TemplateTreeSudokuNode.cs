using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiSudoku
{
    public class TemplateTreeSudokuNode
    {
        public List<TemplateTreeSudokuNode> Childs              { get; set; }
        public TemplateFieldSudokuSquare    TemplateFieldSudoku { get; set; }
        public int                          DirectionFromParent { get; set; }
        public int                          Index               { get; set; }

        public TemplateTreeSudokuNode()
        {
            Childs = new List<TemplateTreeSudokuNode>();
        }
    }
}
