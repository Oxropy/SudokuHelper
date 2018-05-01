using Imms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SudokuHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            int highestValue = 9;
            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = ImportSudoku(pathInput, highestValue);

            if (values.Length > 0)
            {
                Sudoku.PrintSudoku(values, highestValue);

                var groups = Sudoku.GetDefaultGroups();
                Stopwatch sw = Stopwatch.StartNew();
                var solved = Sudoku.Solve(groups, values);
                sw.Stop();
                Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);
                if (solved.IsSome)
                {
                    Sudoku.PrintSudoku(solved.Value, highestValue); 
                }
            }
            Console.ReadKey();
        }

        private static ImmMap<(int row, int column), ImmList<int>> ImportSudoku(string path, int highestValue)
        {
            Dictionary<(int row, int column), ImmList<int>> fields = new Dictionary<(int row, int column), ImmList<int>>();
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    int row = 0;
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        SetRowFieldsOutOfStringLine(fields, line, row, highestValue);
                        row++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Sudoku import error: {0}", ex.Message));
            }
            return fields.ToImmMap();
        }

        private static void SetRowFieldsOutOfStringLine(Dictionary<(int row, int column), ImmList<int>> fields, string line, int row, int highestValue)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (int.TryParse(line[i].ToString(), out int value))
                {
                    fields.Add((row: row, column: i), value == 0 ? Enumerable.Range(1, highestValue).ToImmList() : ImmList.Of(value));
                }
            }
        }
    }

    static class Sudoku
    {
        public static ImmList<ImmList<int>> GetDefaultGroups(int highestValue)
        {
            var groups = new List<ImmList<int>>();
            for (int i = 0; i < highestValue; i++)
            {
                groups.Add(GetRowGroupIndices(i, highestValue));
                groups.Add(GetColumnGroupIndices(i, highestValue));
            }
            return groups.ToImmList();
        }

        public static Optional<ImmMap<(int row, int column), ImmList<int>>> Solve(ImmList<ImmList<(int row, int column)>> groups, ImmMap<(int row, int column), ImmList<int>> fields)
        {
            return Optional.None;
        }

        public static void PrintSudoku(ImmMap<(int row, int column), ImmList<int>> fields, int highestValue)
        {
            for (int i = 0; i < highestValue; i++)
            {
                var row = fields.Where(f => f.Key.row == i).OrderBy(f => f.Key.column);
                foreach (var field in row)
                {
                    Console.Write(GetValueOrDefault(field.Value));
                }
                Console.WriteLine();
            }
        }

        public static int GetValueOrDefault(ImmList<int> field)
        {
            return field.Length == 1 ? field[0] : 0;
        }

        public static ImmList<int> RemoveFixValuesFromPossibleValues(ImmList<int> values, ImmList<int> fixValues)
        {
            return values.Where(v => !fixValues.Contains(v)).ToImmList();
        }

        private static ImmList<int> GetRowGroupIndices(int row, int highestValue)
        {
            var startValue = row * highestValue;
            return GetRowGroupIndices(row, highestValue, startValue, ImmList.Of(startValue));
        }

        private static ImmList<int> GetRowGroupIndices(int row, int highestValue, int value, ImmList<int> values)
        {
            if (values.Length >= highestValue) return values;

            int newValue = value + 1;
            return GetRowGroupIndices(newValue, highestValue, newValue, values.AddLast(newValue));
        }

        private static ImmList<int> GetColumnGroupIndices(int column, int highestValue)
        {
            return GetColumnGroupIndices(column, highestValue, column, ImmList.Of(column));
        }

        private static ImmList<int> GetColumnGroupIndices(int column, int highestValue, int value, ImmList<int> values)
        {
            if (values.Length >= highestValue) return values;

            int newValue = value + highestValue;
            return GetColumnGroupIndices(newValue, highestValue, newValue, values.AddLast(newValue));
        }

        private static ImmList<int> GetSquareGroupIndices(int square, int highestValue, int squareHeight, int squareWidth)
        {
            return null;
        }

    }
}