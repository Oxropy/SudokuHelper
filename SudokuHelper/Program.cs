using Imms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SudokuHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            const char undefined = '_';
            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = SudokuImport.ImportDomainAndSudoku(pathInput, undefined);

            if (values.IsSome)
            {
                var possibleValues = values.Value.Item1;
                var sudokuValues = values.Value.Item2;

                if (sudokuValues.Length > 0)
                {
                    SudokuPrinter.PrintSudoku(sudokuValues, possibleValues.Count);

                    var groups = new HashSet<HashSet<int>>();

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    while (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        var grouped = SudokuImport.ImportGroups(pathInput, undefined, possibleValues.Count);
                        if (grouped.IsSome)
                        {
                            foreach (var group in grouped.Value)
                            {
                                groups.Add(group);
                            }
                        }
                        Console.WriteLine("Group path:");
                        pathInput = Console.ReadLine();
                    }

                    if (groups.Count > 0)
                    {
                        SudokuPrinter.PrintGroups(groups, possibleValues.Count);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(groups, sudokuValues, possibleValues, undefined);
                        sw.Stop();
                        Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);

                        if (solved.IsSome) SudokuPrinter.PrintSudoku(solved.Value, possibleValues.Count);
                        else Console.WriteLine("Unsolved!");
                    }
                }
            }
            Console.ReadKey();
        }
    }

    static class SudokuSolver
    {
        public static Optional<ImmMap<int, char>> Solve(HashSet<HashSet<int>> groups, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined)
        {
            return GetBacktrackedSolvedSudoku(groups, fields, possibleValues, undefined, 0);
        }

        private static Optional<ImmMap<int, char>> GetBacktrackedSolvedSudoku(HashSet<HashSet<int>> groups, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, int index)
        {
            if (!fields.ContainsKey(index)) return fields;
            if (fields[index] != undefined) return GetBacktrackedSolvedSudoku(groups, fields, possibleValues, undefined, index + 1);
            var possible = GetPossibleValues(groups, fields, possibleValues, undefined, index);
            if (possible.Length == 0) return Optional.None;
            return GetBacktrackedSolvedSudoku(groups, fields, possibleValues, undefined, index, possible);
        }

        private static Optional<ImmMap<int, char>> GetBacktrackedSolvedSudoku(HashSet<HashSet<int>> groups, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, int index, ImmList<char> checkValues)
        {
            var nextValue = checkValues.TryFirst;
            if (nextValue.IsNone) return Optional.None;

            var newFields = GetBacktrackedSolvedSudoku(groups, fields.Set(index, nextValue.Value), possibleValues, undefined, index + 1);
            if (newFields.IsNone) return GetBacktrackedSolvedSudoku(groups, fields, possibleValues, undefined, index, checkValues.RemoveFirst());
            return newFields;
        }

        private static ImmList<char> GetPossibleValues(HashSet<HashSet<int>> groups, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, int index)
        {
            var impossibleValues = new HashSet<char>(groups.Where(g => g.Contains(index)) // groups which contains this field
                .SelectMany(g => g)
                .Distinct() // distinct same index
                .Select(g => fields[g]) // get real field value
                .Where(g => g != undefined) // remove unsolved fields
                .Distinct());
            return possibleValues.Where(f => !impossibleValues.Contains(f)).ToImmList();
        }
    }

    static class SudokuImport
    {
        public static Optional<Tuple<HashSet<char>, ImmMap<int, char>>> ImportDomainAndSudoku(string path, char undefined)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var replacedSplit = r.ReadToEnd().Replace("\r\n", "\n").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.ToCharArray()).ToImmList();
                    var possibleValues = new HashSet<char>(replacedSplit.First);
                    var fields = replacedSplit.RemoveFirst().SelectMany(v => v).Select((v, i) => new KeyValuePair<int, char>(i, v)).ToImmMap();

                    if (fields.Any(f => f.Value != undefined && !possibleValues.Contains(f.Value))) return Optional.None;

                    return new Tuple<HashSet<char>, ImmMap<int, char>>(possibleValues, fields);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("´Sudoku import error: {0}", ex.Message));
            }
            return Optional.None;
        }

        public static Optional<HashSet<HashSet<int>>> ImportGroups(string path, char undefined, int length)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var file = r.ReadToEnd().Replace("\r\n", "\n").Replace("\r", "");
                    var groups = Regex.Split(file, "\\n\\n", RegexOptions.IgnorePatternWhitespace)
                        .Select(g => g.Replace("\n", "").ToCharArray()
                        .Select((v, i) => new Tuple<char, int>(v, i))
                        .Where(v => v.Item1 != undefined)
                        .GroupBy(i => i.Item1, i => i.Item2, (k, v) => new KeyValuePair<char, HashSet<int>>(k, new HashSet<int>(v)))
                        .ToImmMap()).SelectMany(g => g.Values);

                    if (groups.Any(g => g.Count != length)) return Optional.None;

                    return new HashSet<HashSet<int>>(groups);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }
            return Optional.None;
        }
    }

    static class SudokuPrinter
    {
        public static void PrintSudoku(ImmMap<int, char> fields, int valueCount)
        {
            for (int i = 0; i < valueCount; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < valueCount; j++)
                {
                    sb.Append(fields[i * valueCount + j]);
                    sb.Append(" ");
                }
                Console.WriteLine(sb.ToString());
            }
            Console.WriteLine();
        }

        public static void PrintGroups(HashSet<HashSet<int>> groups, int valueCount)
        {
            foreach (var group in groups)
            {
                for (int i = 0; i < valueCount; i++)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int j = 0; j < valueCount; j++)
                    {
                        sb.Append(group.Contains(i * valueCount + j) ? "X" : "_");
                        sb.Append(" ");
                    }
                    Console.WriteLine(sb.ToString());
                }
                Console.WriteLine();
            }
        }
    }
}