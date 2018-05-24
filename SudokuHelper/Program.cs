﻿using Imms;
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
            var values = SudokuImport.ImportDomainAndSudoku(pathInput);

            if (values.IsSome)
            {
                var undefined = values.Value.Item1;
                var possibleValues = values.Value.Item2;
                var sudokuValues = values.Value.Item3;

                if (sudokuValues.Length > 0
                    && sudokuValues.All(v => v.Value.Length == possibleValues.Length))
                {
                    SudokuPrinter.PrintSudoku(sudokuValues, possibleValues.Length);

                    var groups = new List<ImmList<int>>();

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    while (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        var group = SudokuImport.ImportGroups(pathInput, undefined, possibleValues);
                        if (group.IsSome)
                        {
                            groups.AddRange(group.Value);
                        }
                        Console.WriteLine("Group path:");
                        pathInput = Console.ReadLine();
                    }

                    if (groups.All(g => g.Length == possibleValues.Length))
                    {
                        SudokuPrinter.PrintGroups(groups.ToImmList(), possibleValues.Length);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(groups.ToImmList(), sudokuValues, possibleValues, undefined);
                        sw.Stop();
                        Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);
                        if (solved.IsSome)
                        {
                            SudokuPrinter.PrintSudoku(solved.Value, possibleValues.Length);
                        }
                        else
                        {
                            Console.WriteLine("Unsolved!");
                        }
                    }
                }
            }
            Console.ReadKey();
        }
    }

    static class SudokuSolver
    {
        public static Optional<ImmMap<int, ImmList<string>>> Solve(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, ImmList<string> possibleValue, string undefined)
        {
            return SolveFields(groups, fields, possibleValue, undefined, 0, fields);
        }

        private static Optional<ImmMap<int, ImmList<string>>> SolveFields(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, ImmList<string> possibleValue, string undefined, int i, ImmMap<int, ImmList<string>> fieldsStartValues)
        {
            var fieldsEndValues = SolveField(groups, fields, possibleValue, undefined, i);

            if (fieldsEndValues.IsNone) return Optional.None;

            if (fields.All(f => f.Value.Length == 1)) return fieldsEndValues;

            SudokuPrinter.PrintSudoku(fields, possibleValue.Length);

            if (!fields.Values.SequenceEqual(fieldsStartValues.Values)) return SolveFields(groups, fields, possibleValue, undefined, 0, fieldsEndValues.Value);

            return GetBacktrackedSolvedSudoku(fields, groups, 0);
        }

        private static Optional<ImmMap<int, ImmList<string>>> SolveField(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, ImmList<string> possibleValue, string undefined, int i)
        {
            if (i < fields.Length)
            {
                var field = fields[i];
                if (field.Length != 1)
                {
                    var groupFields = groups.Where(g => g.Contains(i)).SelectMany(g => g).Distinct();
                    var resultFixField = RemoveFixValuesFromPossibleValues(field, fields.Where(f => groupFields.Contains(f.Key) && f.Value.Length == 1).SelectMany(f => f.Value).Distinct().ToImmList());
                    if (resultFixField.Length == 1) return SolveField(groups, fields.Set(i, resultFixField), possibleValue, undefined, i + 1);

                    var resultUniqueFields = fields.Set(i, GetUniqueValue(groupFields.Select(g => fields[g]).ToImmList(), resultFixField, undefined));
                    return SolveField(groups, resultUniqueFields, possibleValue, undefined, i + 1);
                }
                return SolveField(groups, fields, possibleValue, undefined, i + 1);
            }
            return fields;
        }

        private static ImmList<string> RemoveFixValuesFromPossibleValues(ImmList<string> values, ImmList<string> fixValues)
        {
            return values.Where(v => !fixValues.Contains(v)).ToImmList();
        }

        private static ImmList<string> GetUniqueValue(ImmList<ImmList<string>> groupValues, ImmList<string> field, string undefined)
        {
            var value = groupValues.Where(g => g.Length != 1).SelectMany(g => g).ToList().GroupBy(g => g).ToDictionary(v => v.Key, v => v.ToList().Count).Where(v => v.Value == 1 && field.Contains(v.Key)).FirstOrDefault();
            if (value.Key != null && value.Key != undefined) return ImmList.Of(value.Key);
            return field;
        }

        private static Optional<ImmMap<int, ImmList<string>>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index)
        {
            if (index > fields.Length) return fields;

            if (fields[index].Length == 1) return GetBacktrackedSolvedSudoku(fields, groups, index + 1); // Has value, skip

            return GetBacktrackedSolvedSudoku(fields, groups, index, fields[index]);
        }

        private static Optional<ImmMap<int, ImmList<string>>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, ImmList<string> valuesToCheck)
        {
            if (index > fields.Length) return fields;

            var valueToCheck = valuesToCheck.TryFirst;
            if (valueToCheck.IsNone) return Optional.None;

            if (!IsValueValid(fields, groups, index, valueToCheck.Value)) return GetBacktrackedSolvedSudoku(fields, groups, index, valuesToCheck.RemoveFirst());

            var sudoku = GetBacktrackedSolvedSudoku(fields.Set(index, ImmList.Of(valueToCheck.Value)), groups, index + 1);
            if (sudoku.IsNone) return GetBacktrackedSolvedSudoku(fields, groups, index, valuesToCheck.RemoveFirst());

            return fields;
        }

        private static bool IsValueValid(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, string value)
        {
            return groups.Where(g => g.Contains(index) && g.Length == 1).SelectMany(g => g).Distinct().Select(g => fields[g]).SelectMany(g => g).Distinct().Contains(value);
        }
    }

    static class SudokuImport
    {
        public static Optional<Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>> ImportDomainAndSudoku(string path)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var domain = GetUndefinedAndPossibleValues(r);
                    if (!string.IsNullOrWhiteSpace(r.ReadLine())) return Optional.None; // empty line
                    var sudoku = GetSudoku(r, domain.Item1, domain.Item2);

                    return new Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>(domain.Item1, domain.Item2, sudoku);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("´Sudoku import error: {0}", ex.Message));
            }
            return Optional.None;
        }

        public static Optional<ImmList<ImmList<int>>> ImportGroups(string path, string undefined, ImmList<string> possibleValue)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    return GetGroups(r, undefined);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }

            return Optional.None;
        }

        private static Tuple<string, ImmList<string>> GetUndefinedAndPossibleValues(StreamReader r)
        {
            string line = r.ReadLine();
            if (line == null) return new Tuple<string, ImmList<string>>(string.Empty, ImmList.Of(string.Empty));

            var values = line.Select(v => v.ToString()).ToImmList();
            return new Tuple<string, ImmList<string>>(values.First, values.RemoveFirst());
        }

        private static ImmMap<int, ImmList<string>> GetSudoku(StreamReader r, string undefined, ImmList<string> possibleValues)
        {
            return GetSudoku(r, undefined, possibleValues, new Dictionary<int, ImmList<string>>().ToImmMap(), 0, "");
        }

        private static ImmMap<int, ImmList<string>> GetSudoku(StreamReader r, string undefined, ImmList<string> possibleValues, ImmMap<int, ImmList<string>> fields, int index, string field)
        {
            if (r.EndOfStream) return !string.IsNullOrWhiteSpace(field) ? AddFieldToMap(fields, undefined, possibleValues, index, field) : fields;

            var fieldChar = (char)r.Read();
            if (char.IsWhiteSpace(fieldChar)) return GetSudoku(r, undefined, possibleValues, AddFieldToMap(fields, undefined, possibleValues, index, field), GetIndex(index, field), "");
            return GetSudoku(r, undefined, possibleValues, fields, index, field + fieldChar);
        }

        private static ImmMap<int, ImmList<string>> AddFieldToMap(ImmMap<int, ImmList<string>> fields, string undefined, ImmList<string> possibleValues, int index, string field)
        {
            if (string.IsNullOrWhiteSpace(field)) return fields;
            if (field == undefined) return fields.Add(index, possibleValues.ToImmList());
            return fields.Add(index, ImmList.Of(field));
        }

        private static ImmList<ImmList<int>> GetGroups(StreamReader r, string undefined)
        {
            return GetGroups(r, undefined, new List<ImmList<int>>().ToImmList(), new Dictionary<string, ImmList<int>>().ToImmMap(), 0, "");
        }

        private static ImmList<ImmList<int>> GetGroups(StreamReader r, string undefined, ImmList<ImmList<int>> groups, ImmMap<string, ImmList<int>> groupMap, int index, string group, bool isnewLine = false)
        {
            if (r.EndOfStream)
            {
                if (!string.IsNullOrWhiteSpace(group)) return groups.AddLastRange(AddGroupToMap(groupMap, undefined, index, group).Select(g => g.Value));
                return groupMap.Length > 0 ? groups.AddLastRange(groupMap.Select(g => g.Value)) : groups;
            }

            var groupChar = (char)r.Read();
            if (groupChar == '\n')
            {
                if (isnewLine) return GetGroups(r, undefined, groups.AddLastRange(groupMap.Select(g => g.Value)), new Dictionary<string, ImmList<int>>().ToImmMap(), 0, "");
                return GetGroups(r, undefined, groups, AddGroupToMap(groupMap, undefined, index, group), GetIndex(index, group), "", true);
            }
            if (char.IsWhiteSpace(groupChar)) return GetGroups(r, undefined, groups, AddGroupToMap(groupMap, undefined, index, group), GetIndex(index, group), "");
            return GetGroups(r, undefined, groups, groupMap, index, group + groupChar);
        }

        private static ImmMap<string, ImmList<int>> AddGroupToMap(ImmMap<string, ImmList<int>> groupMap, string undefined, int index, string group)
        {
            if (string.IsNullOrWhiteSpace(group) || group == undefined) return groupMap;
            return groupMap.Set(group, groupMap.ContainsKey(group) ? groupMap[group].AddLast(index) : ImmList.Of(index));
        }

        private static int GetIndex(int index, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return index;
            return index + 1;
        }
    }

    static class SudokuPrinter
    {
        public static void PrintSudoku(ImmMap<int, ImmList<string>> fields, int valueCount)
        {
            for (int i = 0; i < valueCount; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < valueCount; j++)
                {
                    AppendField(sb, fields[i * valueCount + j]);
                    sb.Append(" ");
                }
                Console.WriteLine(sb.ToString());
            }
        }

        public static void PrintGroups(ImmList<ImmList<int>> groups, int valueCount)
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

        private static void AppendField(StringBuilder sb, ImmList<string> field)
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