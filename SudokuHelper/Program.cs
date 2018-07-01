﻿using Imms;
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

            var fieldsWithUnique = GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, fieldsWithPossible.Item1, fieldsWithPossible.Item2, new int[0].ToImmList());
            if (fieldsWithUnique.Item2.Length == 0) return fieldsWithUnique.Item1;

            SudokuPrinter.PrintSudokuWithPossible(fieldsWithPossible.Item1, sudokuValues);

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
                .Select(g => groupIndexToFieldIndex[g])
                .SelectMany(g => g)
                .Where(g => fields[g].Length == 1 && fields[g].First != undefined)
                .Select(g => fields[g].First)
                .ToImmSet();
            return sudokuValues.Except(impossibleValues).ToImmList();
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetUniqueFields(ImmMap<int, HashSet<int>> fieldIndexToGroupIndex, ImmMap<int, HashSet<int>> groupIndexToFieldIndex, ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndexList, ImmList<int> undefinedIndex)
        {
            var index = undefinedIndexList.TryFirst;
            if (index.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, GetStillUndefinedIndex(fields));

            var uniqueValuesInGroups = fieldIndexToGroupIndex[index.Value]
                .Intersect(undefinedIndexList)
                .Select(f => fields[f].Select(v => new Tuple<int, char>(f, v)))
                .SelectMany(f => f)
                .GroupBy(f => f.Item2, f => f.Item1, (k, v) => new KeyValuePair<char, IEnumerable<int>>(k, v))
                .Where(f => f.Value.Count() == 1)
                .Select(f => new KeyValuePair<char, int>(f.Key, f.Value.First()))
                .ToImmList();

            var newFieldsAndUndefinedIndex = GetFieldsWithUniqueUndefined(fields, undefinedIndexList, uniqueValuesInGroups);

            return GetUniqueFields(fieldIndexToGroupIndex, groupIndexToFieldIndex, newFieldsAndUndefinedIndex.Item1, undefinedIndexList.RemoveFirst(), newFieldsAndUndefinedIndex.Item2);
        }

        private static Tuple<ImmMap<int, ImmList<char>>, ImmList<int>> GetFieldsWithUniqueUndefined(ImmMap<int, ImmList<char>> fields, ImmList<int> undefinedIndex, ImmList<KeyValuePair<char, int>> uniqueValues)
        {
            var uniqueValue = uniqueValues.TryFirst;
            if (uniqueValue.IsNone) return new Tuple<ImmMap<int, ImmList<char>>, ImmList<int>>(fields, undefinedIndex);

            var uniqueIndex = undefinedIndex.FindIndex(uniqueValue.Value.Value);
            if (uniqueIndex.IsNone) return GetFieldsWithUniqueUndefined(fields.Set(uniqueValue.Value.Value, ImmList.Of(uniqueValue.Value.Key)), undefinedIndex, uniqueValues.RemoveFirst());

            return GetFieldsWithUniqueUndefined(fields.Set(uniqueValue.Value.Value, ImmList.Of(uniqueValue.Value.Key)), undefinedIndex.RemoveAt(uniqueIndex.Value), uniqueValues.RemoveFirst());
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

        public static void PrintSudokuWithPossible(ImmMap<int, ImmList<char>> fields, HashSet<char> sudokuValues)
        {
            var valueCount = sudokuValues.Count;
            List<ImmMap<Tuple<int, int>, char>> displayFields = new List<ImmMap<Tuple<int, int>, char>>();
            int width = 3;
            int height = 3;

            for (int i = 0; i < valueCount; i++)
            {
                for (int j = 0; j < valueCount; j++)
                {
                    int key = i * valueCount + j;
                    displayFields.Add(GetFieldPossibleValueGrid(width, height, fields[key], sudokuValues.ToImmList()));
                }
            }

            var orderedFields = displayFields.SelectMany(f => f).Select((f, i) => new Tuple<int, int, char>(f.Key.Item1 * i, f.Key.Item2 * width, f.Value)).OrderBy(f => f.Item1).ThenBy(f => f.Item2).ToImmList();

            for (int i = 0; i < orderedFields.Length; i++)
            {
                var field = orderedFields[i];
                StringBuilder sb = new StringBuilder();
                sb.Append(field.Item3);
                sb.Append(" ");
                if ((i + 1) % (sudokuValues.Count * width) == 0)
                {
                    Console.WriteLine(sb); 
                }
                else
                {
                    Console.Write(sb);
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

        private static ImmMap<Tuple<int, int>, char> GetFieldPossibleValueGrid(int width, int height, ImmList<char> field, ImmList<char> sudokuValues)
        {
            int sudokuValueIndex = 0;

            Dictionary<Tuple<int, int>, char> fieldValues = new Dictionary<Tuple<int, int>, char>();

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int fieldIndex = i * sudokuValues.Length + j;
                    char value = sudokuValues[sudokuValueIndex];
                    if (!field.Contains(value))
                    {
                        value = '_';
                    }

                    fieldValues.Add(new Tuple<int, int>(i * fieldIndex * height, j + fieldIndex * width), value);
                    sudokuValueIndex++;
                }
            }

            return fieldValues.ToImmMap();
        }
    }
}