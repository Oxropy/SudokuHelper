using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SudokuHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Square count in row:");
            string input = Console.ReadLine();
            int.TryParse(input, out int squareRowCount);

            Console.WriteLine("Square count in column:");
            input = Console.ReadLine();
            int.TryParse(input, out int squareColumnCount);

            Console.WriteLine("Square height:");
            input = Console.ReadLine();
            int.TryParse(input, out int squareHeight);

            Console.WriteLine("Square width:");
            input = Console.ReadLine();
            int.TryParse(input, out int squareWidth);

            if (squareRowCount > 0 && squareHeight > 0 && squareWidth > 0)
            {
                int maxValue = squareHeight * squareWidth;

                Console.WriteLine("Split rows by '|'");
                Console.WriteLine("0 or space for unknown numbers");
                input = Console.ReadLine();

                string[] lines = input.Split('|');

                int[][] rows = new int[maxValue][];
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i] = GetIntValuesOutOfStringLine(squareColumnCount * squareWidth, lines[i]);
                }

                Sudoku sudoku = new Sudoku(rows, squareRowCount, squareColumnCount, squareHeight, squareWidth);

                Stopwatch sw = Stopwatch.StartNew();
                SudokuHandler.SolveSudoku(sudoku);
                sw.Stop();
                Console.WriteLine("Time: {0}ms, {1}ticks", sw.ElapsedMilliseconds, sw.ElapsedTicks);
                SudokuHandler.PrintSudoku(sudoku);
            }
            Console.ReadKey();
        }

        private static int[] GetIntValuesOutOfStringLine(int maxValues, string line)
        {
            int[] values = new int[9];
            for (int i = 0; i < 9; i++)
            {
                if (!int.TryParse(line[i].ToString(), out values[i]))
                {
                    values[i] = 0;
                }
            }
            return values;
        }
    }

    static class SudokuHandler
    {
        public static void SolveSudoku(Sudoku sudoku)
        {
            int solveRun = 0;
            while (!sudoku.IsSolved())
            {
                solveRun++;

                for (int i = 0; i < sudoku.MaxValue; i++)
                {
                    SolveRow(sudoku, i);
                    SolveColumn(sudoku, i);
                }

                for (int i = 0; i < sudoku.SquareRowCount; i++)
                {
                    for (int j = 0; j < sudoku.SquareColumnCount; j++)
                    {
                        SolveSquare(sudoku, i, j); 
                    }
                }
            }
            Console.WriteLine("Needed runs: {0}", solveRun);
        }

        private static void SolveRow(Sudoku sudoku, int row)
        {
            RemoveImpossibleValues(sudoku.Fields.Where(f => f.Row == row));
        }

        private static void SolveColumn(Sudoku sudoku, int column)
        {
            RemoveImpossibleValues(sudoku.Fields.Where(f => f.Column == column));
        }

        private static void SolveSquare(Sudoku sudoku, int squareRow, int squareColumn)
        {
            int lowestRow = sudoku.SquareWidth * squareRow;
            int heighestRow = (sudoku.SquareWidth * (squareRow + 1)) - 1;
            int lowestColumn = sudoku.SquareHeight * squareColumn;
            int heighestColumn = (sudoku.SquareHeight * (squareColumn + 1)) - 1;

            RemoveImpossibleValues(sudoku.Fields.Where(f => (f.Row >= lowestRow && f.Row <= heighestRow && f.Column >= lowestColumn && f.Column <= heighestColumn)));
        }

        private static void RemoveImpossibleValues(IEnumerable<SudokuField> fields)
        {
            var values = fields.Where(f => f.IsSet()).Select(f => f.GetValue());
            var fieldsToSet = fields.Where(f => !f.IsSet()).Select(f => f as SudokuInputField);

            foreach (var field in fieldsToSet)
            {
                field.RemoveImpossibleValues(values);
            }
        }

        public static void PrintSudoku(Sudoku sudoku)
        {
            int fieldWidth = sudoku.MaxValue.ToString().Length;

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < sudoku.MaxValue; i++)
            {
                if (i % sudoku.SquareHeight == 0)
                {
                    Console.WriteLine();
                }

                IEnumerable<SudokuField> row = sudoku.Fields.Where(f => f.Row == i).OrderBy(f => f.Column).OrderBy(f => f.Row);
                foreach (var field in row)
                {
                    if (field.Column > 0 && field.Column % sudoku.SquareWidth == 0)
                    {
                        sb.Append(" ");
                    }
                    sb.Append(field.GetValue().ToString().PadLeft(fieldWidth + 1));
                }
                Console.WriteLine(sb);
                sb.Clear();
            }
        }
    }

    class Sudoku
    {
        public readonly int MaxValue;
        public readonly int SquareRowCount;
        public readonly int SquareColumnCount;
        public readonly int SquareHeight;
        public readonly int SquareWidth;
        public readonly List<SudokuField> Fields = new List<SudokuField>();

        public Sudoku(int[][] values, int squareRowCount, int squareColumnCount, int squareHeight, int squareWidth)
        {
            SquareRowCount = squareRowCount;
            SquareColumnCount = squareColumnCount;
            SquareHeight = squareHeight;
            SquareWidth = squareWidth;
            MaxValue = squareHeight * squareWidth;

            for (int i = 0; i < values.Length; i++)
            {
                for (int j = 0; j < values[i].Length; j++)
                {
                    int value = values[i][j];
                    if (value == 0)
                    {
                        Fields.Add(new SudokuInputField(i, j, MaxValue)); 
                    }
                    else
                    {
                        Fields.Add(new SudokuFixValue(i, j, value));
                    }
                }
            }
        }

        public bool IsSolved()
        {
            return !Fields.Any(f => !f.IsSet());
        }
    }

    abstract class SudokuField
    {
        public readonly int Row;
        public readonly int Column;

        protected SudokuField(int row, int column)
        {
            Row = row;
            Column = column;
        }

        abstract public int GetValue();
        abstract public bool IsSet();

        public override string ToString()
        {
            return string.Format("({0}:{1}): {2}", Row, Column, GetValue());
        }
    }

    class SudokuFixValue : SudokuField
    {
        public readonly int Value;

        public SudokuFixValue(int row, int column, int value) : base(row, column)
        {
            Value = value;
        }

        public override int GetValue()
        {
            return Value;
        }

        public override bool IsSet()
        {
            return true;
        }
    }

    class SudokuInputField : SudokuField
    {
        private readonly int MaxValue;

        private bool isSet;
        public readonly List<int> PossibleValues = new List<int>();

        public SudokuInputField(int row, int column, int maxValue) : base(row, column)
        {
            MaxValue = maxValue;
            SetAllValuesPossible();
        }

        public override int GetValue()
        {
            if (PossibleValues.Count == 1)
            {
                return PossibleValues[0];
            }
            return 0;
        }

        public override bool IsSet()
        {
            return isSet;
        }

        public void RemoveImpossibleValues(IEnumerable<int> impossible)
        {
            foreach (var value in impossible)
            {
                PossibleValues.Remove(value);
            }

            CheckIsSet();
        }

        private void CheckIsSet()
        {
            if (PossibleValues.Count == 0)
            {
                SetAllValuesPossible();
            }

            isSet = PossibleValues.Count == 1;
        }

        private void SetAllValuesPossible()
        {
            for (int i = 1; i <= MaxValue; i++)
            {
                PossibleValues.Add(i);
            }
        }
    }
}