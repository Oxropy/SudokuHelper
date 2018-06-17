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
                        var fieldIndexToGroupIndex = groups.Value.Item1;
                        var groupIndexToFieldIndex = groups.Value.Item2;

                        SudokuPrinter.PrintGroups(groupIndexToFieldIndex, possibleValues.Count);

                        //TestCycles(100, fieldindexToGroupIndex, groupIndexToFieldIndex, sudokuValues, possibleValues, undefined, undefinedIndex, undefinedValues);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(fieldIndexToGroupIndex, groupIndexToFieldIndex, sudokuValues, possibleValues, undefined, undefinedIndex);
                        sw.Stop();
                        Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);

                        if (solved.IsSome) SudokuPrinter.PrintSudoku(solved.Value, possibleValues.Count);
                        else Console.WriteLine("Unsolved!");
                    }
                }
            }
            Console.ReadKey();
        }

        static void TestCycles(int times, ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            for (int i = 0; i < times; i++)
            {
                SudokuSolver.Solve(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex);
            }
        }
    }

    static class SudokuSolver
    {
        public static Optional<ImmMap<int, ImmList<char>>> Solve(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            var fieldsWithPossible = GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex);
            if (fieldsWithPossible.Item2.Length == 0) return fieldsWithPossible.Item1;

            var fieldsWithUnique = GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fieldsWithPossible.Item1, fieldsWithPossible.Item2);
            if (fieldsWithUnique.Item2.Length == 0) return fieldsWithUnique.Item1;
            
            return GetBacktracked(fieldIndexToGroupIndex, groupIndexToFieldIndex, fieldsWithUnique.Item1, fieldsWithUnique.Item2);
        }

        private static Optional<ImmMap<int, ImmList<char>>> GetBacktracked(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex)
        {
            var undefinedIndexValue = undefinedIndex.TryFirst;
            if (undefinedIndexValue.IsNone) return fields;

            return GetBacktrackedUndefined(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, undefinedIndex);
        }

        private static Optional<ImmMap<int, ImmList<char>>> GetBacktrackedUndefined(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex)
        {
            var nextValue = fields[undefinedIndex.First].TryFirst;
            if (nextValue.IsNone) return Optional.None;

            var newFields = GetBacktracked(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields.Set(undefinedIndex.First, fields[undefinedIndex.First].RemoveFirst()), undefinedIndex.RemoveFirst());
            if (newFields.IsSome) return newFields;
            return GetBacktrackedUndefined(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields.Set(undefinedIndex.First, fields[undefinedIndex.First].RemoveFirst()), undefinedIndex);
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetPossibleFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, GetStillUndefinedIndex(fields, undefinedIndex, new int[0].ToImmList()));

            var impossibleValues = GetImpossibleIndex(fieldIndexToGroupIndex, groupIndexToFieldIndex, undefined, undefinedIndex);
            var possibleValue = GetPossibleValues(fields, possibleValues, impossibleValues);
            return GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields.Set(index.Value, possibleValue), possibleValues, undefined, undefinedIndex.RemoveFirst());
        }

        private static ImmSet<int> GetImpossibleIndex(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, char undefined, ImmList<int> undefinedIndex)
        {
            return fieldIndexToGroupIndex[undefinedIndex.First]
                .Select(g => groupIndexToFieldIndex[g])
                .SelectMany(g => g)
                .Except(undefinedIndex)
                .ToImmSet();
        }

        private static ImmList<char> GetPossibleValues(ImmMap<int, ImmList<char>> fields, HashSet<char> possibleValues, ImmSet<int> impossibleIndex)
        {
            var impossibleValues = impossibleIndex.Select(g => fields[g].First).ToImmSet();
            return possibleValues.Except(impossibleValues).ToImmList();
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetUniqueFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, GetStillUndefinedIndex(fields, undefinedIndex, new int[0].ToImmList()));

            var valuesInGroups = fieldIndexToGroupIndex[index.Value].Intersect(undefinedIndex).Select(f => fields[f].Select(v => new Tuple<int, char>(f, v)));

            var uniqueValuesInGroups = valuesInGroups.SelectMany(f => f)
                .GroupBy(f => f.Item2, f => f.Item1, (k, v) => new KeyValuePair<char, IEnumerable<int>>(k, v))
                .Where(f => f.Value.Count() == 1)
                .Select(f => new KeyValuePair<char, int>(f.Key, f.Value.First()))
                .ToImmList();

            var newFieldsAndUndefinedIndex = GetFieldsWithUniqueUndefined(fields, undefinedIndex, uniqueValuesInGroups);

            return GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, newFieldsAndUndefinedIndex.Item1, newFieldsAndUndefinedIndex.Item2);
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetFieldsWithUniqueUndefined(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<KeyValuePair<char, int>> unique)
        {
            var uniqueValue = unique.TryFirst;
            if (uniqueValue.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            return GetFieldsWithUniqueUndefined(fields.Set(uniqueValue.Value.Value, ImmList.Of(uniqueValue.Value.Key)), undefinedIndex.RemoveAt(undefinedIndex.FindIndex(uniqueValue.Value.Value).Value), unique.RemoveFirst());
        }

        private static ImmList<int> GetStillUndefinedIndex(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<int> stillUndefinedIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone) return stillUndefinedIndex;

            if (fields[index.Value].Length > 1) return GetStillUndefinedIndex(fields, undefinedIndex.RemoveFirst(), stillUndefinedIndex.AddLast(index.Value));
            return GetStillUndefinedIndex(fields, undefinedIndex.RemoveFirst(), stillUndefinedIndex);
        }
    }

    static class SudokuImport
    {
        public static Optional<Tuple<HashSet<char>, ImmMap<int, ImmList<char>>, ImmList<int>>> ImportDomainAndSudoku(string path, char undefined)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var replacedSplit = r.ReadToEnd().Replace("\r\n", "\n").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.ToCharArray()).ToImmList();
                    var possibleValues = new HashSet<char>(replacedSplit.First);
                    var fields = replacedSplit.RemoveFirst().SelectMany(v => v).Select((v, i) => new KeyValuePair<int, ImmList<char>>(i, ImmList.Of(v))).ToImmMap();

                    if (fields.Any(f => f.Value.First != undefined && !possibleValues.Contains(f.Value.First))) return Optional.None;
                    var undefinedIndex = fields.Where(f => f.Value.First == undefined).Select(f => f.Key).ToImmList();

                    return new Tuple<HashSet<char>, ImmMap<int, ImmList<char>>, ImmList<int>>(possibleValues, fields, undefinedIndex);
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
        public static void PrintSudoku(ImmMap<int, ImmList<char>> fields, int valueCount)
        {
            for (int i = 0; i < valueCount; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < valueCount; j++)
                {
                    int key = i * valueCount + j;
                    AppendField(sb, fields[key]);
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

        private static void AppendField(StringBuilder sb, ImmList<char> field)
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
    }
}