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
        public static ImmList<ImmList<Tuple<int, int>>> GetDefaultGroups()
        {
            var square1 = ImmList.Of(
                new Tuple<int,int>(0,0),
                new Tuple<int,int>(0,1),
                new Tuple<int,int>(0,2),
                new Tuple<int,int>(1,1),
                new Tuple<int,int>(1,2),
                new Tuple<int,int>(1,3),
                new Tuple<int,int>(2,1),
                new Tuple<int,int>(2,2),
                new Tuple<int,int>(2,3)
            );
            
            var square2 = ImmList.Of(
                new Tuple<int,int>(4,0),
                new Tuple<int,int>(4,1),
                new Tuple<int,int>(4,2),
                new Tuple<int,int>(5,1),
                new Tuple<int,int>(5,2),
                new Tuple<int,int>(5,3),
                new Tuple<int,int>(6,1),
                new Tuple<int,int>(6,2),
                new Tuple<int,int>(6,3)
            );
            
            var square3 = ImmList.Of(
                new Tuple<int,int>(7,0),
                new Tuple<int,int>(7,1),
                new Tuple<int,int>(7,2),
                new Tuple<int,int>(8,1),
                new Tuple<int,int>(8,2),
                new Tuple<int,int>(8,3),
                new Tuple<int,int>(9,1),
                new Tuple<int,int>(9,2),
                new Tuple<int,int>(9,3)
            );
            
            var square4 = ImmList.Of(
                new Tuple<int,int>(0,4),
                new Tuple<int,int>(0,5),
                new Tuple<int,int>(0,6),
                new Tuple<int,int>(1,4),
                new Tuple<int,int>(1,5),
                new Tuple<int,int>(1,6),
                new Tuple<int,int>(2,4),
                new Tuple<int,int>(2,5),
                new Tuple<int,int>(2,6)
            );
            
            var square5 = ImmList.Of(
                new Tuple<int,int>(3,4),
                new Tuple<int,int>(3,5),
                new Tuple<int,int>(3,6),
                new Tuple<int,int>(4,4),
                new Tuple<int,int>(4,5),
                new Tuple<int,int>(4,6),
                new Tuple<int,int>(5,4),
                new Tuple<int,int>(5,5),
                new Tuple<int,int>(5,6)
            );
            
            var square6 = ImmList.Of(
                new Tuple<int,int>(6,4),
                new Tuple<int,int>(6,5),
                new Tuple<int,int>(6,6),
                new Tuple<int,int>(7,4),
                new Tuple<int,int>(7,5),
                new Tuple<int,int>(7,6),
                new Tuple<int,int>(8,4),
                new Tuple<int,int>(8,5),
                new Tuple<int,int>(8,6)
            );

            var square7 = ImmList.Of(
                new Tuple<int,int>(0,7),
                new Tuple<int,int>(0,8),
                new Tuple<int,int>(0,9),
                new Tuple<int,int>(1,7),
                new Tuple<int,int>(1,8),
                new Tuple<int,int>(1,9),
                new Tuple<int,int>(2,7),
                new Tuple<int,int>(2,8),
                new Tuple<int,int>(2,9)
            );
            
            var square8 = ImmList.Of(
                new Tuple<int,int>(4,7),
                new Tuple<int,int>(4,8),
                new Tuple<int,int>(4,9),
                new Tuple<int,int>(5,7),
                new Tuple<int,int>(5,8),
                new Tuple<int,int>(5,9),
                new Tuple<int,int>(6,7),
                new Tuple<int,int>(6,8),
                new Tuple<int,int>(6,9)
            );
            
            var square9 = ImmList.Of(
                new Tuple<int,int>(7,7),
                new Tuple<int,int>(7,8),
                new Tuple<int,int>(7,9),
                new Tuple<int,int>(8,7),
                new Tuple<int,int>(8,8),
                new Tuple<int,int>(8,9),
                new Tuple<int,int>(9,7),
                new Tuple<int,int>(9,8),
                new Tuple<int,int>(9,9)
            );

            var groups = new List<ImmList<Tuple<int,int>>>();
            for (int i = 0; i < 9; i++)
            {
                var row = ImmList.Of(
                    new Tuple<int,int>(i,0),
                    new Tuple<int,int>(i,1),
                    new Tuple<int,int>(i,2),
                    new Tuple<int,int>(i,3),
                    new Tuple<int,int>(i,4),
                    new Tuple<int,int>(i,5),
                    new Tuple<int,int>(i,6),
                    new Tuple<int,int>(i,7),
                    new Tuple<int,int>(i,8)
                );
                groups.Add(row);

                var column = ImmList.Of(
                    new Tuple<int,int>(0,i),
                    new Tuple<int,int>(1,i),
                    new Tuple<int,int>(2,i),
                    new Tuple<int,int>(3,i),
                    new Tuple<int,int>(4,i),
                    new Tuple<int,int>(5,i),
                    new Tuple<int,int>(6,i),
                    new Tuple<int,int>(7,i),
                    new Tuple<int,int>(8,i)
                );
                groups.Add(column);
            }

            return ImmList.Of(square1, square2, square3, square4, square5, square6, square7, square8, square9).AddLastRange(groups);
        }

        public static Optional<ImmMap<(int row, int column), ImmList<int>>> Solve(ImmList<ImmList<Tuple<int, int>>> groups, ImmMap<(int row, int column), ImmList<int>> fields)
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
    }
}