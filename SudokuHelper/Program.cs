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
            var values = Sudoku.ImportDomainAndSudoku(pathInput);

            if (values.IsSome)
            {
                var undefined = values.Value.Item1;
                var possibleValues = values.Value.Item2;
                var sudokuValues = values.Value.Item3;

                if (sudokuValues.Length > 0)
                {
                    Sudoku.PrintSudoku(sudokuValues, possibleValues.Length);

                    var groups = new List<ImmList<int>>();

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    while (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        var group = Sudoku.ImportGroups(pathInput, undefined, possibleValues);
                        if (group.IsSome)
                        {
                            groups.AddRange(group.Value); 
                        }
                        Console.WriteLine("Group path:");
                        pathInput = Console.ReadLine();
                    }
                    Sudoku.PrintGroups(groups.ToImmList(), possibleValues.Length);

                    Stopwatch sw = Stopwatch.StartNew();
                    var solved = Sudoku.Solve(groups.ToImmList(), sudokuValues, possibleValues, undefined);
                    sw.Stop();
                    Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);
                    Sudoku.PrintSudoku(solved, possibleValues.Length);
                } 
            }
            Console.ReadKey();
        }
    }

    static class Sudoku
    {
        public static Optional<Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>> ImportDomainAndSudoku(string path)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var domain = GetUndefinedAndPossibleValues(r);
                    r.ReadLine(); // empty line
                    var sudoku = GetSudoku(r, domain.Item1, domain.Item2);

                    if (sudoku.IsNone) return Optional.None;
                    
                    return new Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>(domain.Item1, domain.Item2, sudoku.Value);
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
                    return GetGroups(r, undefined, possibleValue);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }

            return Optional.None;
        }

        public static ImmMap<int, ImmList<string>> Solve(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, ImmList<string> possibleValue, string undefined)
        {
            return SolveField(groups, fields, possibleValue, undefined, 0);
        }

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

        private static ImmMap<int, ImmList<string>> SolveField(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, ImmList<string> possibleValue, string undefined, int i)
        {
            if (i < fields.Length)
            {
                var field = fields[i];
                if (field.Length != 1)
                {
                    var groupFields = groups.Where(g => g.Contains(i)).SelectMany(g => g).Distinct();
                    var resultFixField = RemoveFixValuesFromPossibleValues(field, fields.Where(f => groupFields.Contains(f.Key) && f.Value.Length == 1).SelectMany(f => f.Value).Distinct().ToImmList());
                    if (resultFixField.Length == 1)
                    {
                        var resultFixFields = fields.Remove(i).Add(i, resultFixField);
                        return SolveField(groups, resultFixFields, possibleValue, undefined, i + 1);
                    }

                    var resultUniqueField = GetUniqueValue(groupFields.Select(g => fields[g]).ToImmList(), resultFixField, undefined);
                    var resultUniqueFields = fields.Remove(i).Add(i, resultUniqueField);
                    return SolveField(groups, resultUniqueFields, possibleValue, undefined, i + 1);
                }
                return SolveField(groups, fields, possibleValue, undefined, i + 1);
            }
            if (fields.Any(f => f.Value.Length > 1))
            {
                PrintSudoku(fields, possibleValue.Length);
                return SolveField(groups, fields, possibleValue, undefined, 0);
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
            if (value.Key != undefined) return ImmList.Of(value.Key);
            return field;
        }

        private static ImmMap<int, ImmList<string>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index)
        {
            if (index > fields.Length) return fields;

            if (fields[index].Length == 1) return GetBacktrackedSolvedSudoku(fields, groups, index + 1); // Has value, skip

            return GetBacktrackedSolvedSudoku(fields, groups, index, fields[index]);
        }

        private static ImmMap<int, ImmList<string>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, ImmList<string> valuesToCheck)
        {
            if (index > fields.Length) return fields;

            if (IsValueValid(fields, groups, index, valuesToCheck.First)) return GetBacktrackedSolvedSudoku(fields, groups, index + 1);

            return GetBacktrackedSolvedSudoku(fields, groups, index, valuesToCheck.RemoveFirst());
        }

        private static bool IsValueValid(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, string value)
        {
            return groups.Where(g => g.Contains(index) && g.Length == 1).SelectMany(g => g).Distinct().Select(g => fields[g]).SelectMany(g => g).Distinct().Contains(value);
        }

        private static Tuple<string, ImmList<string>> GetUndefinedAndPossibleValues(StreamReader reader)
        {
            string line = reader.ReadLine();
            if (line != null)
            {
                var values = line.ToCharArray().Select(v => v.ToString()).ToImmList();
                return new Tuple<string, ImmList<string>>(values.First, values.RemoveFirst());
            }

            return new Tuple<string, ImmList<string>>(string.Empty, ImmList.Of(string.Empty));
        }

        private static Optional<ImmMap<int, ImmList<string>>> GetSudoku(StreamReader reader, string undefined, ImmList<string> possibleValues)
        {
            string line = reader.ReadLine();
            if (line == null) return Optional.None;
            
            int row = 0;
            return GetSudoku(reader, undefined, possibleValues, ImmMap.Of(GetFieldValuesOfLine(line, undefined, possibleValues, row)), row + 1);
        }

        private static ImmMap<int, ImmList<string>> GetSudoku(StreamReader reader, string undefined, ImmList<string> possibleValues, ImmMap<int, ImmList<string>> sudoku, int row)
        {
            string line = reader.ReadLine();
            if (line == null) return sudoku;

            return GetSudoku(reader, undefined, possibleValues, sudoku.AddRange(GetFieldValuesOfLine(line, undefined, possibleValues, row)), row + 1);
        }

        private static KeyValuePair<int, ImmList<string>>[] GetFieldValuesOfLine(string line, string undefined, ImmList<string> possibleValue, int row)
        {
            return line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v)).Select((v, i) => new KeyValuePair<int, ImmList<string>>(i + row * possibleValue.Length, v == undefined ? possibleValue : ImmList.Of(v))).ToArray();
        }

        private static Optional<ImmList<ImmList<int>>> GetGroups(StreamReader reader, string undefined, ImmList<string> possibleValue)
        {
            string line = reader.ReadLine();
            if (line == null) return Optional.None;

            int row = 0;
            return GetGroups(reader, undefined, possibleValue, GetGroupValuesOfLine(line, undefined, possibleValue, row), row + 1);
        }

        private static ImmList<ImmList<int>> GetGroups(StreamReader reader, string undefined, ImmList<string> possibleValue, ImmMap<string, ImmList<int>> groups, int row)
        {
            string line = reader.ReadLine();
            if (line == null) return groups.Values.ToImmList();

            return GetGroups(reader, undefined, possibleValue, GetGroupValuesOfLine(line, undefined, possibleValue, groups, row), row + 1);
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOfLine(string line, string undefined, ImmList<string> possibleValue, int row)
        {
            Dictionary<string, ImmList<int>> groupMap = new Dictionary<string, ImmList<int>>();
            line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v) && v != undefined).Select((v, i) => new Tuple<string, int>(v, i)).ToList().ForEach(v => { if (groupMap.ContainsKey(v.Item1)) { groupMap[v.Item1] = groupMap[v.Item1].AddLast(v.Item2); } else { groupMap.Add(v.Item1, ImmList.Of(v.Item2)); } });

            return groupMap.ToImmMap();
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOfLine(string line, string undefined, ImmList<string> possibleValue, ImmMap<string, ImmList<int>> groups, int row)
        {
            Dictionary<string, ImmList<int>> groupMap = new Dictionary<string, ImmList<int>>();
            foreach (var item in groups)
            {
                groupMap.Add(item.Key, item.Value);
            }

            line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v) && v != undefined).Select((v, i) => new Tuple<string, int>(v, i)).ToList().ForEach(v => { if (groupMap.ContainsKey(v.Item1)) { groupMap[v.Item1] = groupMap[v.Item1].AddLast(v.Item2); } else { groupMap.Add(v.Item1, ImmList.Of(v.Item2)); } });

            return groups.ToImmMap();
        }
    }
}