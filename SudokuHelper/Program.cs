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
            var definedFields = GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, possibleValues, undefined, undefinedIndex);
            if (definedFields.Item1.IsNone) return Optional.None;
            if (definedFields.Item2.Length == 0) return definedFields.Item1;

            return GetBacktracked(fieldIndexToGroupIndex, groupIndexToFieldIndex, definedFields.Item1.Value, definedFields.Item2);
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

        private static Tuple<Optional<ImmMap<int, ImmList<char>>>, ImmList<int>> GetPossibleFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> possibleValues, char undefined, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone)
                return new Tuple<Optional<ImmMap<int, ImmList<char>>>, ImmList<int>>(fields, undefinedIndex);

            var possibleFieldValues = GetPossibleValues(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, undefined, undefinedIndex, possibleValues);
            if (possibleFieldValues.Length == 0)
                return new Tuple<Optional<ImmMap<int, ImmList<char>>>, ImmList<int>>(Optional.None, undefinedIndex);

            var newPossibleFields = GetFieldsWithNewValues(fields, undefined, possibleFieldValues.Select(v => new KeyValuePair<char, int>(v, index.Value)).ToImmList());
            var newUndefinedIndex = GetUndefinedIndex(newPossibleFields, undefinedIndex, new List<int>().ToImmList());

            if (possibleFieldValues.Length == 1)
                return GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, newPossibleFields, possibleValues, undefined, newUndefinedIndex);

            var groupValues = fieldIndexToGroupIndex[index.Value]
                .Select(f => groupIndexToFieldIndex[f])
                .SelectMany(f => f).Intersect(undefinedIndex)
                .Select(f => newPossibleFields[f].Select(v => new Tuple<int, char>(f, v)))
                .SelectMany(f => f);

            if (groupValues.Any(f => f.Item2 == undefined))
                return GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, newPossibleFields, possibleValues, undefined, newUndefinedIndex);

            var uniqueValuesInGroups = groupValues.GroupBy(f => f.Item2, f => f.Item1, (k, v) => new KeyValuePair<char, IEnumerable<int>>(k, v))
                .Where(f => f.Value.Count() == 1)
                .Select(f => new KeyValuePair<char, int>(f.Key, f.Value.First()))
                .ToImmList();

            var newFields = GetFieldsWithNewValues(newPossibleFields, undefined, uniqueValuesInGroups);
            var newÍndex = GetUndefinedIndex(newFields, undefinedIndex, new List<int>().ToImmList());
            return GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, newFields, possibleValues, undefined, newÍndex);
        }

        private static ImmList<char> GetPossibleValues(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, char undefined, ImmList<int> undefinedIndex, HashSet<char> possibleValues)
        {
            var impossibleValues = fieldIndexToGroupIndex[undefinedIndex.First]
                .Select(g => groupIndexToFieldIndex[g])
                .SelectMany(g => g)
                .Except(undefinedIndex)
                .Select(g => fields[g].First).ToImmSet();
            return possibleValues.Except(impossibleValues).ToImmList();
        }

        private static ImmMap<int, ImmList<char>> GetFieldsWithNewValues(ImmMap<int, ImmList<char>> fields, char undefined, ImmList<KeyValuePair<char, int>> possibleValues)
        {
            var possibleValue = possibleValues.TryFirst;
            if (possibleValue.IsNone)
                return fields;

            var fieldsWithoutUndefined = fields[possibleValue.Value.Value].First == undefined ? fields.Set(possibleValue.Value.Value, fields[possibleValue.Value.Value].RemoveFirst()) : fields;

            return GetFieldsWithNewValues(fieldsWithoutUndefined.Set(possibleValue.Value.Value, fieldsWithoutUndefined[possibleValue.Value.Value].AddLast(possibleValue.Value.Key)), undefined, possibleValues.RemoveFirst());
        }

        private static ImmList<int> GetUndefinedIndex(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<int> newIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone)
                return newIndex;

            if (fields[index.Value].Length == 1)
                return GetUndefinedIndex(fields, undefinedIndex.RemoveFirst(), newIndex.AddLast(index.Value));

            return GetUndefinedIndex(fields, undefinedIndex.RemoveFirst(), newIndex);
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