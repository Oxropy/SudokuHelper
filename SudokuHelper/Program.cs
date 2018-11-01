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

            string sudokuPath;
            string groupsPath;
            if (args.Length == 0)
            {
                Console.WriteLine("Sudoku path:");
                sudokuPath = Console.ReadLine();

                Console.WriteLine("Group path:");
                groupsPath = Console.ReadLine();
            }
            else
            {
                sudokuPath = args[0];
                groupsPath = args[1];
            }
            var values = SudokuImport.ImportDomainAndSudoku(sudokuPath, undefined);

            if (values.IsSome)
            {
                var sudokuValues = values.Value.Item1;
                var sudokuField = values.Value.Item2;
                var undefinedIndex = values.Value.Item3;

                var groups = SudokuImport.ImportGroups(groupsPath, undefined, sudokuValues.Count);

                if (sudokuField.Length > 0 && groups.IsSome)
                {
                    SudokuPrinter.PrintSudoku(sudokuField, sudokuValues.Count);

                    //TestCycles(100, sudokuField, groups.Value, sudokuValues, undefinedIndex);

                    Stopwatch sw = Stopwatch.StartNew();
                    var solved = SudokuSolver.Solve(sudokuField, groups.Value, sudokuValues, undefinedIndex);
                    sw.Stop();
                    Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);

                    if (solved.IsSome) SudokuPrinter.PrintSudoku(solved.Value, sudokuValues.Count);
                    else Console.WriteLine("Unsolved!");
                }
            }
            Console.ReadKey();
        }

        static void TestCycles(int times, ImmMap<int, ImmList<char>> fields, ImmList<ImmMap<char, HashSet<int>>> groups, HashSet<char> sudokuValues, ImmList<int> undefinedIndex)
        {
            for (int i = 0; i < times; i++)
            {
                SudokuSolver.Solve(fields, groups, sudokuValues, undefinedIndex);
            }
        }
    }

    static class SudokuSolver
    {
        /// <summary>
        /// Solves Sudoku.
        /// </summary>
        /// <returns>Solved fields or None.</returns>
        public static Optional<ImmMap<int, ImmList<char>>> Solve(ImmMap<int, ImmList<char>> fields, ImmList<ImmMap<char, HashSet<int>>> groups, HashSet<char> values, ImmList<int> undefinedIndex)
        {
            var fieldToRelatedFields = SudokuHelper.GetMappingFieldToRelatedFields(groups);

            return Solve(fields, fieldToRelatedFields, values, undefinedIndex);
        }

        /// <summary>
        /// Solves Sudoku.
        /// </summary>
        /// <returns>Solved fields or None.</returns>
        public static Optional<ImmMap<int, ImmList<char>>> Solve(ImmMap<int, ImmList<char>> fields, ImmMap<int, HashSet<int>> fieldToRelatedFields, HashSet<char> values, ImmList<int> undefinedIndex)
        {
            var fieldsWithPossible = SudokuHelper.GetUndefinedAsPossibleFields(fields, values, undefinedIndex);

            if (fieldsWithPossible.IsNone) return Optional.None;
            return SolveBacktracked(fieldsWithPossible.Value, fieldToRelatedFields, values, undefinedIndex);
        }

        /// <summary>
        /// Solves Sudoku backtracked. Get next undefined index.
        /// </summary>
        /// <returns>Solved fields or None.</returns>
        private static Optional<ImmMap<int, ImmList<char>>> SolveBacktracked(ImmMap<int, ImmList<char>> fields, ImmMap<int, HashSet<int>> fieldToRelatedFields, HashSet<char> values, ImmList<int> undefinedIndex)
        {
            var nextUndefinedIndex = undefinedIndex.TryFirst;
            if (nextUndefinedIndex.IsNone) return fields;

            return SolveBacktrackedUndefined(fields, fieldToRelatedFields, values, undefinedIndex);
        }

        /// <summary>
        /// Solves Sudoku backtracked. Use first value of undefined index.
        /// </summary>
        /// <returns>Solved fields or None.</returns>
        private static Optional<ImmMap<int, ImmList<char>>> SolveBacktrackedUndefined(ImmMap<int, ImmList<char>> fields, ImmMap<int, HashSet<int>> fieldToRelatedFields, HashSet<char> values, ImmList<int> undefinedIndex)
        {
            var possibleValues = fields[undefinedIndex.First].Length == values.Count ? GetPossibleValues(fields, fieldToRelatedFields, values, undefinedIndex.First) : fields[undefinedIndex.First];
            if (possibleValues.Length == 0) return Optional.None;

            var newFields = SolveBacktracked(fields.Set(undefinedIndex.First, ImmList.Of(possibleValues.First)), fieldToRelatedFields, values, undefinedIndex.RemoveFirst());
            if (newFields.IsNone) return SolveBacktrackedUndefined(fields.Set(undefinedIndex.First, possibleValues.RemoveFirst()), fieldToRelatedFields, values, undefinedIndex);
            return newFields;
        }

        /// <summary>
        /// Gets possible values for one field.
        /// </summary>
        /// <returns>List with possible values.</returns>
        public static ImmList<char> GetPossibleValues(ImmMap<int, ImmList<char>> fields, ImmMap<int, HashSet<int>> fieldToRelatedFields, HashSet<char> values, int fieldIndex)
        {
            var impossibleValues = fieldToRelatedFields[fieldIndex]
                .Where(g => fields[g].Length == 1)
                .Select(g => fields[g].First)
                .ToImmSet();
            return values.Except(impossibleValues).ToImmList();
        }
    }

    static class SudokuHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groups"></param>
        /// <returns></returns>
        public static ImmMap<int, HashSet<int>> GetMappingFieldToRelatedFields(IEnumerable<ImmMap<char, HashSet<int>>> groups)
        {
            var groupToFields = GetMappingGroupToFields(groups);
            var fieldsToGroups = GetMappingFieldToGroup(groupToFields);
            return fieldsToGroups.Select(f => new KeyValuePair<int, HashSet<int>>(f.Key, new HashSet<int>(f.Value.SelectMany(v => groupToFields[v])))).ToImmMap();
        }

        /// <summary>
        /// Gets map which fields are in which groups.
        /// </summary>
        /// <param name="groups"></param>
        /// <returns>Map of which fields are in which groups.</returns>
        public static ImmMap<int, HashSet<int>> GetMappingGroupToFields(IEnumerable<ImmMap<char, HashSet<int>>> groups)
        {
            return groups.SelectMany((g, gi) => g.Select((v, i) => new KeyValuePair<int, HashSet<int>>(i + (gi * 10), v.Value))).ToImmMap();
        }

        /// <summary>
        /// Gets map which field is in which groups.
        /// </summary>
        /// <param name="groupToField"></param>
        /// <returns>Map of which field is in which groups.</returns>
        public static ImmMap<int, HashSet<int>> GetMappingFieldToGroup(ImmMap<int, HashSet<int>> groupToField)
        {
            return groupToField.SelectMany(g => g.Value.Select(v => new Tuple<int, int>(v, g.Key)))
                        .GroupBy(g => g.Item1, g => g.Item2, (k, v) => new KeyValuePair<int, HashSet<int>>(k, new HashSet<int>(v)))
                        .ToImmMap();
        }

        /// <summary>
        /// Set possible values in undefined values.
        /// </summary>
        /// <returns>Fields with possible values excanged undefined value.</returns>
        public static Optional<ImmMap<int, ImmList<char>>> GetUndefinedAsPossibleFields(ImmMap<int, ImmList<char>> fields, HashSet<char> values, ImmList<int> undefinedIndex)
        {
            var nextIndex = undefinedIndex.TryFirst;
            if (nextIndex.IsNone) return fields;

            return GetUndefinedAsPossibleFields(fields.Set(nextIndex.Value, values.ToImmList()), values, undefinedIndex.RemoveFirst());
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

        public static Optional<ImmList<ImmMap<char, HashSet<int>>>> ImportGroups(string path, char undefined, int length)
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
                        .ToImmMap())
                        .ToImmList();

                    if (groups.Any(g => g.Values.Any(v => v.Count != length))) return Optional.None;

                    return groups;
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