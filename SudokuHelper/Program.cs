using Imms;
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
            int highestValue = 9;
            int squareColumnCount = 3;
            int squareRowCount = 3;
            int squareHeight = 3;
            int squareWidth = 3;

            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = ImportSudoku(pathInput, highestValue);

            if (values.Length > 0)
            {
                Sudoku.PrintSudoku(values, highestValue);

                var groups = Sudoku.GetDefaultGroups(highestValue, squareColumnCount, squareRowCount, squareHeight, squareWidth);
                Stopwatch sw = Stopwatch.StartNew();
                var solved = Sudoku.Solve(groups, values, highestValue);
                sw.Stop();
                Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);
                Sudoku.PrintSudoku(solved, highestValue); 
            }
            Console.ReadKey();
        }

        private static ImmMap<int, ImmList<int>> ImportSudoku(string path, int highestValue)
        {
            Dictionary<int, ImmList<int>> fields = new Dictionary<int, ImmList<int>>();
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

        private static void SetRowFieldsOutOfStringLine(Dictionary<int, ImmList<int>> fields, string line, int row, int highestValue)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (int.TryParse(line[i].ToString(), out int value))
                {
                    fields.Add(row * highestValue + i, value == 0 ? Enumerable.Range(1, highestValue).ToImmList() : ImmList.Of(value));
                }
            }
        }
    }

    static class Sudoku
    {
        public static ImmList<ImmList<int>> GetDefaultGroups(int highestValue, int squareColumnCount, int squareRowCount, int squareHeight, int squareWidth)
        {
            var groups = new List<ImmList<int>>();
            for (int i = 0; i < highestValue; i++)
            {
                groups.Add(GetRowGroupIndices(i, highestValue));
                groups.Add(GetColumnGroupIndices(i, highestValue));
            }
            for (int row = 0; row < squareRowCount; row++)
            {
                for (int column = 0; column < squareColumnCount; column++)
                {
                    int startValue = column * squareWidth + row * squareHeight * highestValue;
                    groups.Add(GetSquareGroupIndices(startValue, highestValue, squareHeight, squareWidth)); 
                }
            }
            return groups.ToImmList();
        }

        public static ImmMap<int, ImmList<int>> Solve(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<int>> fields, int highestValue)
        {
            while (true)
            {
                var newFields = SolveField(groups, fields, 0);
                if (newFields.All(f => f.Value.Length == 1))
                {
                    return newFields;
                }
                PrintSudoku(fields, highestValue);
            }
        }

        public static void PrintSudoku(ImmMap<int, ImmList<int>> fields, int highestValue)
        {
            for (int i = 0; i < highestValue; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < highestValue; j++)
                {
                    AppendField(sb, fields[i * highestValue + j]);
                    sb.Append(" ");
                }
                Console.WriteLine(sb.ToString());
            }
        }

        private static void AppendField(StringBuilder sb, ImmList<int> field)
        {
            if (field.Length == 1)
            {
                sb.Append(field[0]);
            }
            else
            {
                sb.Append("[");
                sb.Append(string.Join(",", field));
                sb.Append("]");
            }
        }

        private static ImmMap<int, ImmList<int>> SolveField(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<int>> fields, int i)
        {
            if (i >= fields.Length) return fields;

            var field = fields[i];
            if (field.Length > 1)
            {
                var newField = RemoveFixValuesFromPossibleValues(field, groups.Where(g => g.Contains(i) && fields[i].Length == 1).SelectMany(g => g).ToImmList());
                var newFields = fields.Remove(i).Add(i, newField);
                return SolveField(groups, newFields, i + 1); 
            }
            return SolveField(groups, fields, i + 1);
        }

        private static ImmList<int> RemoveFixValuesFromPossibleValues(ImmList<int> values, ImmList<int> fixValues)
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

        private static ImmList<int> GetSquareGroupIndices(int squareStartValue, int highestValue, int squareHeight, int squareWidth)
        {
            var result = new List<int>();
            for (int i = 0; i < squareHeight; i++)
            {
                int value = squareStartValue + i * highestValue;
                for (int j = 0; j < squareWidth; j++)
                {
                    result.Add(value + j);
                }
            }
            return result.ToImmList();
        }
    }
}