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
                var sudokuValues = values.Value.Item1;
                var sudokuField = values.Value.Item2;
                var undefinedIndex = values.Value.Item3;

                if (sudokuField.Length > 0)
                {
                    SudokuPrinter.PrintSudoku(sudokuField, sudokuValues.Count);

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();

                    var groups = SudokuImport.ImportGroups(pathInput, undefined, sudokuValues.Count, 0);

                    if (groups.IsSome)
                    {
                        var fieldIndexToGroupIndex = groups.Value.Item1;
                        var groupIndexToFieldIndex = groups.Value.Item2;

                        SudokuPrinter.PrintGroups(groupIndexToFieldIndex, sudokuValues.Count);

                        //TestCycles(100, fieldIndexToGroupIndex, groupIndexToFieldIndex, sudokuField, sudokuValues, undefined, undefinedIndex);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(fieldIndexToGroupIndex, groupIndexToFieldIndex, sudokuField, sudokuValues, undefined, undefinedIndex);
                        sw.Stop();
                        Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);

                        if (solved.IsSome) SudokuPrinter.PrintSudoku(solved.Value, sudokuValues.Count);
                        else Console.WriteLine("Unsolved!");
                    }
                }
            }
            Console.ReadKey();
        }

        static void TestCycles(int times, ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues, char undefined, ImmList<int> undefinedIndex)
        {
            for (int i = 0; i < times; i++)
            {
                SudokuSolver.Solve(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, sudokuValues, undefined, undefinedIndex);
            }
        }
    }

    static class SudokuSolver
    {
        public static Optional<ImmMap<int, ImmList<char>>> Solve(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues, char undefined, ImmList<int> undefinedIndex)
        {
            var fieldsWithPossible = GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, sudokuValues, undefined, undefinedIndex);
            if (fieldsWithPossible.Item2.Length == 0) return fieldsWithPossible.Item1;

            SudokuPrinter.PrintSudokuWithPossible(fieldsWithPossible.Item1, sudokuValues);

            var fieldsWithUnique = GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fieldsWithPossible.Item1, fieldsWithPossible.Item2, fieldsWithPossible.Item2);
            if (fieldsWithUnique.Item2.Length == 0) return fieldsWithUnique.Item1;

            SudokuPrinter.PrintSudokuWithPossible(fieldsWithUnique.Item1, sudokuValues);

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

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetPossibleFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues, char undefined, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndex.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, GetStillUndefinedIndex(fields));

            var possibleValues = GetPossibleValues(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields, sudokuValues, undefined, index.Value);
            return GetPossibleFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fields.Set(index.Value, possibleValues), sudokuValues, undefined, undefinedIndex.RemoveFirst());
        }

        private static ImmList<char> GetPossibleValues(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues, char undefined, int currentUndefinedIndex)
        {
            var impossibleValues = fieldIndexToGroupIndex[currentUndefinedIndex]
                .SelectMany(g => groupIndexToFieldIndex[g])
                .Where(g => fields[g].Length == 1 && fields[g].First != undefined)
                .Select(g => fields[g].First)
                .ToImmSet();
            return sudokuValues.Except(impossibleValues).ToImmList();
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetUniqueFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndexList, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndexList.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var uniqueValuesInGroups = fieldIndexToGroupIndex[index.Value]
                .SelectMany(g => groupIndexToFieldIndex[g]
                .Intersect(undefinedIndex)
                .SelectMany(f => fields[f].Select(v => new Tuple<int, char>(f, v)))
                .GroupBy(f => f.Item2, f => f.Item1, (k, v) => new Tuple<char, IEnumerable<int>>(k, v))
                .Where(f => f.Item2.Count() == 1)
                .Select(f => new Tuple<char, int>(f.Item1, f.Item2.First())))
            .Distinct()
            .ToImmList();

            var newFieldsAndUndefinedIndex = SetUniqueFields(fields, undefinedIndexList, uniqueValuesInGroups);
            var removedUniqueValuesFromUndefined = RemoveValueFromUndefined(fieldIndexToGroupIndex, groupIndexToFieldIndex, newFieldsAndUndefinedIndex.Item1, newFieldsAndUndefinedIndex.Item2, uniqueValuesInGroups);

            SudokuPrinter.PrintSudokuWithPossible(removedUniqueValuesFromUndefined.Item1, new HashSet<char>() { '1', '2', '3', '4', '5', '6', '7', '8', '9' });

            return GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, removedUniqueValuesFromUndefined.Item1, undefinedIndexList.RemoveFirst(), removedUniqueValuesFromUndefined.Item2);
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> SetUniqueFields(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<Tuple<char, int>> uniqueValues)
        {
            var uniqueValue = uniqueValues.TryFirst;
            if (uniqueValue.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var uniqueIndex = undefinedIndex.FindIndex(uniqueValue.Value.Item2);
            if (uniqueIndex.IsNone) return SetUniqueFields(fields.Set(uniqueValue.Value.Item2, ImmList.Of(uniqueValue.Value.Item1)), undefinedIndex, uniqueValues.RemoveFirst());

            return SetUniqueFields(fields.Set(uniqueValue.Value.Item2, ImmList.Of(uniqueValue.Value.Item1)), undefinedIndex.RemoveAt(uniqueIndex.Value), uniqueValues.RemoveFirst());
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> RemoveValueFromUndefined(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<Tuple<char, int>> uniqueValues)
        {
            var uniqueValue = uniqueValues.TryFirst;
            if (uniqueValue.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var undefinedFieldsOfGroup = fieldIndexToGroupIndex[uniqueValue.Value.Item2]
                .SelectMany(g => groupIndexToFieldIndex[g])
                .Intersect(undefinedIndex)
                .ToImmList();

            var removedUniqueInOtherUndefined = RemoveValueFromUndefined(fields, undefinedIndex, uniqueValue.Value, undefinedFieldsOfGroup);
            var removedUniqueValues = RemoveUniqueValues(fields, uniqueValues, new Tuple<char, int>[0].ToImmList());

            return RemoveValueFromUndefined(fieldIndexToGroupIndex, groupIndexToFieldIndex, removedUniqueInOtherUndefined.Item1, removedUniqueInOtherUndefined.Item2, removedUniqueValues);
        }

        private static ImmList<Tuple<char, int>> RemoveUniqueValues(ImmMap<int, ImmList<char>> fields, ImmList<Tuple<char, int>> uniqueValues, ImmList<Tuple<char, int>> newUniqueValues)
        {
            var index = uniqueValues.TryFirst;
            if (index.IsNone) return newUniqueValues;

            if (fields[index.Value.Item2].Length == 1) return RemoveUniqueValues(fields, uniqueValues.RemoveFirst(), newUniqueValues);

            return RemoveUniqueValues(fields, uniqueValues.RemoveFirst(), newUniqueValues.AddLast(index.Value));
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> RemoveValueFromUndefined(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, Tuple<char, int> uniqueValue, ImmList<int> fieldsForUpdateCheck)
        {
            var index = fieldsForUpdateCheck.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var removedValue = RemoveValueFromFieldAndUndefined(fields, undefinedIndex, index.Value, uniqueValue.Item1);

            return RemoveValueFromUndefined(removedValue.Item1, removedValue.Item2, uniqueValue, fieldsForUpdateCheck.RemoveFirst());
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> RemoveValueFromFieldAndUndefined(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, int index, char value)
        {
            var indexOfValue = fields[index].FindIndex(value);
            if (indexOfValue.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var fieldValue = fields[index].RemoveAt(indexOfValue.Value);
            var newFields = fields.Set(index, fieldValue);
            if (fieldValue.Length != 0) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(newFields, undefinedIndex);

            var undefinedIndexIndex = undefinedIndex.FindIndex(index);
            return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(newFields, undefinedIndex.RemoveAt(undefinedIndexIndex.Value));
        }

        private static ImmList<int> GetStillUndefinedIndex(ImmMap<int, ImmList<char>> fields)
        {
            return fields.Where(f => f.Value.Length > 1).Select(f => f.Key).ToImmList();
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

                    var groupIndexToFieldIndex = groups.SelectMany((g, gi) => g.Select((v, i) => new KeyValuePair<int, HashSet<int>>(i + (gi * 10) + (groupindexStart * 100), v.Value)))
                        .ToImmMap();

                    var fieldindexToGroupIndex = groupIndexToFieldIndex.SelectMany(g => g.Value.Select(v => new Tuple<int, int>(v, g.Key)))
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

        public static void PrintSudokuWithPossible(ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues)
        {
            var valueCount = sudokuValues.Count;
            var displayFields = new List<ImmMap<int, char>>();
            int width = 3;
            int height = 3;

            for (int row = 0; row < valueCount; row++)
            {
                for (int column = 0; column < valueCount; column++)
                {
                    int fieldIndex = row * valueCount + column;
                    displayFields.Add(GetFieldPossibleValueGrid(row, column, width, height, fields[fieldIndex], sudokuValues.ToImmList()));
                }
            }

            var orderedFields = displayFields.SelectMany(f => f).Select(f => new Tuple<int, char>(f.Key, f.Value)).OrderBy(f => f.Item1).ToImmList();

            for (int i = 0; i < orderedFields.Length; i++)
            {
                var field = orderedFields[i];
                StringBuilder sb = new StringBuilder();

                sb.Append(field.Item2);
                sb.Append(" ");

                if ((i + 1) % width == 0)
                {
                    if ((i + 1) % (width * width) == 0) sb.Append("|");
                    sb.Append("| ");
                }

                Console.Write(sb);

                if ((i + 1) % (sudokuValues.Count) == 0)
                {
                    if ((i + 1) % (sudokuValues.Count * width) == 0)
                    {
                        Console.WriteLine();
                        if ((i + 1) % (sudokuValues.Count * height * height) == 0)
                        {
                            Console.WriteLine();
                            if ((i + 1) % (sudokuValues.Count * height * height * height) == 0) Console.WriteLine();
                        }
                    }
                }
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
                sb.Append("_");
            }
        }

        private static ImmMap<int, char> GetFieldPossibleValueGrid(int row, int column, int width, int height, ImmList<char> field, ImmList<char> sudokuValues)
        {
            int sudokuValueIndex = 0;
            var fieldValues = new Dictionary<int, char>();

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    char value = sudokuValues[sudokuValueIndex];
                    if (!field.Contains(value)) value = '_';

                    int index = (row * sudokuValues.Length * sudokuValues.Length) + (column * width) + i * width * sudokuValues.Length + j;
                    fieldValues.Add(index, value);
                    sudokuValueIndex++;
                }
            }
            return fieldValues.ToImmMap();
        }
    }
}