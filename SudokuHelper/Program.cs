using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SudokuHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = ImportSudoku(pathInput);

            if (values.Length > 0)
            {
                Sudoku sudoku = new Sudoku(values);

                Stopwatch sw = Stopwatch.StartNew();
                SudokuHandler.SolveSudoku(sudoku);
                sw.Stop();
                Console.WriteLine("Time: {0}ms, {1}ticks", sw.ElapsedMilliseconds, sw.ElapsedTicks);
                SudokuHandler.PrintSudoku(sudoku);
            }
            Console.ReadKey();
        }

        private static int[][] ImportSudoku(string path)
        {
            var values = new List<int[]>();
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        values.Add(GetIntValuesOutOfStringLine(line));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Sudoku import error: {0}", ex.Message));
            }
            return values.ToArray();
        }

        private static int[] GetIntValuesOutOfStringLine(string line)
        {
            var values = new List<int>();
            for (int i = 0; i < line.Length; i++)
            {
                if (int.TryParse(line[i].ToString(), out int value))
                {
                    values.Add(value);
                }

            }
            return values.ToArray();
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

            //RemoveImpossibleValuesCauseFieldsHasOnlyTheseValues(fieldsToSet);
        }

        private static void RemoveImpossibleValuesCauseFieldsHasOnlyTheseValues(IEnumerable<SudokuField> fields)
        {
            IEnumerable<SudokuInputField> fieldsToSet = fields.Where(f => !f.IsSet()).Select(f => f as SudokuInputField);
            if (fieldsToSet.Count() > 0)
            {
                var fieldsPossibleValues = fieldsToSet.Where(f => !f.IsSet()).Select(f => f.PossibleValues);
                var fieldsPossibleValuesDistinct = fieldsPossibleValues.Distinct();

                var valueCount = fieldsPossibleValuesDistinct.Select(f => new Tuple<List<int>, int>(f, fieldsPossibleValues.Count(v => v.SequenceEqual(f))));

                if (valueCount.Count() > 1)
                {
                    var valueCountOrdered = valueCount.Where(v => v.Item2 > 1).OrderBy(c => c.Item2);
                    foreach (var valueConstellation in valueCountOrdered)
                    {
                        if (valueConstellation.Item1.Count == valueConstellation.Item2)
                        {
                            var fieldsWithImpossibleValues = fieldsToSet.Where(f => !f.PossibleValues.SequenceEqual(valueConstellation.Item1));
                            foreach (var fieldWithImpossibleValues in fieldsWithImpossibleValues)
                            {
                                fieldWithImpossibleValues.RemoveImpossibleValues(valueConstellation.Item1);
                            }
                        }
                    }
                }
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

        public Sudoku(int[][] values)
        {
            SquareRowCount = 3;
            SquareColumnCount = 3;
            SquareHeight = 3;
            SquareWidth = 3;
            MaxValue = 9;

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
                        Fields.Add(new SudokuField(i, j, value));
                    }
                }
            }
        }

        public bool IsSolved()
        {
            return !Fields.Any(f => !f.IsSet());
        }
    }

    class SudokuField
    {
        protected int Value;
        public readonly int Row;
        public readonly int Column;

        public SudokuField(int row, int column, int value = 0)
        {
            Row = row;
            Column = column;
            Value = value;
        }

        public int GetValue()
        {
            return Value;
        }

        public bool IsSet()
        {
            return Value != 0;
        }

        public override string ToString()
        {
            return string.Format("({0}:{1}): {2}", Row, Column, Value);
        }
    }

    class SudokuInputField : SudokuField
    {
        private readonly int MaxValue;
        public readonly List<int> PossibleValues = new List<int>();

        public SudokuInputField(int row, int column, int maxValue) : base(row, column)
        {
            MaxValue = maxValue;
            SetAllValuesPossible();
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
            Value = GetValueOrDefault();
        }

        private void SetAllValuesPossible()
        {
            for (int i = 1; i <= MaxValue; i++)
            {
                PossibleValues.Add(i);
            }
        }

        private int GetValueOrDefault()
        {
            if (PossibleValues.Count == 1)
            {
                return PossibleValues[0];
            }
            return 0;
        }
    }
}