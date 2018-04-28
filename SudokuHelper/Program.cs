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
            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = ImportSudoku(pathInput);

            if (values.Length > 0)
            {
                Sudoku sudoku = new Sudoku(values, SudokuHandler.GetDefaultSquares());
                SudokuHandler.PrintSudoku(sudoku);

                //Stopwatch sw = Stopwatch.StartNew();
                //SudokuHandler.SolveSudoku(sudoku);
                //sw.Stop();
                //Console.WriteLine("Time: {0}ms, {1}ticks", sw.ElapsedMilliseconds, sw.ElapsedTicks);
                //SudokuHandler.PrintSudoku(sudoku);
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
        public static List<List<Tuple<int, int>>> GetDefaultSquares()
        {
            List<List<Tuple<int, int>>> groups = new List<List<Tuple<int, int>>>();

            var square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(0,0),
                new Tuple<int,int>(0,1),
                new Tuple<int,int>(0,2),
                new Tuple<int,int>(1,1),
                new Tuple<int,int>(1,2),
                new Tuple<int,int>(1,3),
                new Tuple<int,int>(2,1),
                new Tuple<int,int>(2,2),
                new Tuple<int,int>(2,3)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(4,0),
                new Tuple<int,int>(4,1),
                new Tuple<int,int>(4,2),
                new Tuple<int,int>(5,1),
                new Tuple<int,int>(5,2),
                new Tuple<int,int>(5,3),
                new Tuple<int,int>(6,1),
                new Tuple<int,int>(6,2),
                new Tuple<int,int>(6,3)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(7,0),
                new Tuple<int,int>(7,1),
                new Tuple<int,int>(7,2),
                new Tuple<int,int>(8,1),
                new Tuple<int,int>(8,2),
                new Tuple<int,int>(8,3),
                new Tuple<int,int>(9,1),
                new Tuple<int,int>(9,2),
                new Tuple<int,int>(9,3)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(0,4),
                new Tuple<int,int>(0,5),
                new Tuple<int,int>(0,6),
                new Tuple<int,int>(1,4),
                new Tuple<int,int>(1,5),
                new Tuple<int,int>(1,6),
                new Tuple<int,int>(2,4),
                new Tuple<int,int>(2,5),
                new Tuple<int,int>(2,6)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(3,4),
                new Tuple<int,int>(3,5),
                new Tuple<int,int>(3,6),
                new Tuple<int,int>(4,4),
                new Tuple<int,int>(4,5),
                new Tuple<int,int>(4,6),
                new Tuple<int,int>(5,4),
                new Tuple<int,int>(5,5),
                new Tuple<int,int>(5,6)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(6,4),
                new Tuple<int,int>(6,5),
                new Tuple<int,int>(6,6),
                new Tuple<int,int>(7,4),
                new Tuple<int,int>(7,5),
                new Tuple<int,int>(7,6),
                new Tuple<int,int>(8,4),
                new Tuple<int,int>(8,5),
                new Tuple<int,int>(8,6)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(0,7),
                new Tuple<int,int>(0,8),
                new Tuple<int,int>(0,9),
                new Tuple<int,int>(1,7),
                new Tuple<int,int>(1,8),
                new Tuple<int,int>(1,9),
                new Tuple<int,int>(2,7),
                new Tuple<int,int>(2,8),
                new Tuple<int,int>(2,9)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(4,7),
                new Tuple<int,int>(4,8),
                new Tuple<int,int>(4,9),
                new Tuple<int,int>(5,7),
                new Tuple<int,int>(5,8),
                new Tuple<int,int>(5,9),
                new Tuple<int,int>(6,7),
                new Tuple<int,int>(6,8),
                new Tuple<int,int>(6,9)
            };
            groups.Add(square);

            square = new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(7,7),
                new Tuple<int,int>(7,8),
                new Tuple<int,int>(7,9),
                new Tuple<int,int>(8,7),
                new Tuple<int,int>(8,8),
                new Tuple<int,int>(8,9),
                new Tuple<int,int>(9,7),
                new Tuple<int,int>(9,8),
                new Tuple<int,int>(9,9)
            };
            groups.Add(square);

            for (int i = 0; i < 9; i++)
            {
                var row = new List<Tuple<int, int>>()
                {
                    new Tuple<int,int>(i,0),
                    new Tuple<int,int>(i,1),
                    new Tuple<int,int>(i,2),
                    new Tuple<int,int>(i,3),
                    new Tuple<int,int>(i,4),
                    new Tuple<int,int>(i,5),
                    new Tuple<int,int>(i,6),
                    new Tuple<int,int>(i,7),
                    new Tuple<int,int>(i,8)
                };
                groups.Add(row);

                var column = new List<Tuple<int, int>>()
                {
                    new Tuple<int,int>(0,i),
                    new Tuple<int,int>(1,i),
                    new Tuple<int,int>(2,i),
                    new Tuple<int,int>(3,i),
                    new Tuple<int,int>(4,i),
                    new Tuple<int,int>(5,i),
                    new Tuple<int,int>(6,i),
                    new Tuple<int,int>(7,i),
                    new Tuple<int,int>(8,i)
                };
                groups.Add(column);
            }

            return groups;
        }

        public static void SolveSudoku(Sudoku sudoku)
        {

        }

        public static void PrintSudoku(Sudoku sudoku)
        {
            IEnumerable<Field> fields = sudoku.Groups.SelectMany(g => g.Fields).Distinct();

            for (int i = 0; i < sudoku.Groups.Count; i++)
            {
                IEnumerable<Field> row = fields.Where(f => f.Row == i).OrderBy(f => f.Column);
                foreach (var position in row)
                {
                    int value = position.PossibleValues.Length == 1 ? position.PossibleValues[0] : 0;
                    Console.Write(value);
                }
                Console.WriteLine();
            }
        }
    }

    class Sudoku
    {
        public readonly List<Group> Groups;

        public Sudoku(int[][] values, List<List<Tuple<int, int>>> groups)
        {
            List<Field> fields = new List<Field>();
            for (int i = 0; i < values.Length; i++)
            {
                for (int j = 0; j < values[i].Length; j++)
                {
                    Field field;
                    int value = values[i][j];
                    if (value == 0)
                    {
                        field = new Field(i, j, Enumerable.Range(1, values.Length).ToImmList());
                    }
                    else
                    {
                        field = new Field(i, j, ImmList.Of(value));
                    }
                    fields.Add(field);
                }
            }

            Groups = new List<Group>();
            foreach (var group in groups)
            {
                List<Field> fieldList = new List<Field>();
                foreach (var field in fields)
                {
                    foreach (var position in group)
                    {
                        if (position.Item1 == field.Row && position.Item2 == field.Column)
                        {
                            fieldList.Add(field);
                        }
                    }
                }
                Groups.Add(new Group(fieldList));
            }
        }

        public static bool IsGroupComplete(Group group)
        {
            return group.Fields.All(f => f.PossibleValues.Count() == 1);
        }
    }

    struct Group
    {
        public readonly ImmList<Field> Fields;

        public Group(IEnumerable<Field> fields)
        {
            Fields = fields.ToImmList();
        }
    }

    struct Field
    {
        public readonly int Row;
        public readonly int Column;
        public readonly ImmList<int> PossibleValues;

        public Field(int row, int column, ImmList<int> possibleValues)
        {
            Row = row;
            Column = column;
            PossibleValues = possibleValues;
        }
    }
}