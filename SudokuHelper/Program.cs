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
                var undefinedIndex = values.Value.Item3;

                if (sudokuValues.Length > 0)
                {
                    SudokuPrinter.PrintSudoku(sudokuValues, possibleValues.Count);

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();

                    var groups = SudokuImport.ImportGroups(pathInput, undefined, possibleValues.Count, 0);
                    
                    if (groups.IsSome)
                    {
                        var fieldindexToGroupIndex = groups.Value.Item1;
                        var groupIndexToFieldIndex = groups.Value.Item2;

                        SudokuPrinter.PrintGroups(groupIndexToFieldIndex, possibleValues.Count);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(fieldindexToGroupIndex, groupIndexToFieldIndex, sudokuValues, possibleValues, undefined, undefinedIndex);
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
        public static Optional<ImmMap<int, char>> Solve(ImmMap<int, HashSet<int>> fieldindexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            return GetBacktracked(fieldindexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex);
        }

        public static Optional<ImmMap<int, char>> GetBacktracked(ImmMap<int, HashSet<int>> fieldindexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            var undefinedIndexValue = undefinedIndex.TryFirst;
            if (undefinedIndexValue.IsNone) return fields;

            var possible = GetPossibleValues(fieldindexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndexValue.Value);
            return GetBacktrackedUndefined(fieldindexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex, possible);
        }

        private static Optional<ImmMap<int, char>> GetBacktrackedUndefined(ImmMap<int, HashSet<int>> fieldindexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex, ImmList<char> checkValues)
        {
            var nextValue = checkValues.TryFirst;
            if (nextValue.IsNone) return Optional.None;

            var newFields = GetBacktracked(fieldindexToGroupIndex, groupIndexToFieldIndex, fields.Set(undefinedIndex.First, nextValue.Value), possibleValues, undefined, undefinedIndex.RemoveFirst());
            if (newFields.IsNone) return GetBacktrackedUndefined(fieldindexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex, checkValues.RemoveFirst());
            return newFields;
        }

        private static ImmList<char> GetPossibleValues(ImmMap<int, HashSet<int>> fieldindexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, char> fields, HashSet<char> possibleValues, char undefined, int index)
        {
            var impossibleValues = fieldindexToGroupIndex[index]
                .Select(g => groupIndexToFieldIndex[g])
                .SelectMany(g => g)
                .Select(g => fields[g])
                .Where(g => g != undefined)
                .ToImmSet();

            return possibleValues.Where(f => !impossibleValues.Contains(f)).ToImmList();
        }
    }

    static class SudokuImport
    {
        public static Optional<Tuple<HashSet<char>, ImmMap<int, char>, ImmList<int>>> ImportDomainAndSudoku(string path, char undefined)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var replacedSplit = r.ReadToEnd().Replace("\r\n", "\n").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.ToCharArray()).ToImmList();
                    var possibleValues = new HashSet<char>(replacedSplit.First);
                    var fields = replacedSplit.RemoveFirst().SelectMany(v => v).Select((v, i) => new KeyValuePair<int, char>(i, v)).ToImmMap();

                    if (fields.Any(f => f.Value != undefined && !possibleValues.Contains(f.Value))) return Optional.None;
                    var undefinedIndex = fields.Where(f => f.Value == undefined).Select(f => f.Key).ToImmList();

                    return new Tuple<HashSet<char>, ImmMap<int, char>, ImmList<int>>(possibleValues, fields, undefinedIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Sudoku import error: {0}", ex.Message);
            }
            return Optional.None;
        }

        public static Optional<Tuple<ImmMap<int, HashSet<int>>, ImmMap<int, HashSet<int>>>> ImportGroups(string path, char undefined, int length, int groupindexStart)
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
                        .ToImmMap());

                    var groupIndexToFieldIndex = groups.Select((g, gi) => g.Select((v, i) => new KeyValuePair<int, HashSet<int>>(i + (gi * 10) + (groupindexStart * 100), v.Value)))
                        .SelectMany(g => g)
                        .ToImmMap();

                    var fieldindexToGroupIndex = groupIndexToFieldIndex.Select(g => g.Value.Select(v => new Tuple<int, int>(v, g.Key)))
                        .SelectMany(g => g)
                        .GroupBy(g => g.Item1, g => g.Item2, (k, v) => new KeyValuePair<int, HashSet<int>>(k, new HashSet<int>(v)))
                        .ToImmMap();

                    if (groupIndexToFieldIndex.Any(g => g.Value.Count != length)) return Optional.None;

                    return new Tuple<ImmMap<int, HashSet<int>>, ImmMap<int, HashSet<int>>>(fieldindexToGroupIndex, groupIndexToFieldIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Group import error: {0}", ex.Message);
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

        public static void PrintGroups(ImmMap<int, HashSet<int>> groupIndexToFieldIndex, int valueCount)
        {
            var groups = groupIndexToFieldIndex.Values.ToImmList();

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