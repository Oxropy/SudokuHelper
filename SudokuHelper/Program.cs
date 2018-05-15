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
            var values = Sudoku.GetDomain(pathInput);
            if (values.Length > 1)
            {
                var undefined = values.First;
                var possibleValues = values.RemoveFirst();
                var sudokuValues = Sudoku.ImportSudoku(pathInput, possibleValues, undefined);

                if (sudokuValues.Length > 0)
                {
                    Sudoku.PrintSudoku(sudokuValues, possibleValues.Length);

                    var groups = new List<ImmList<int>>();

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    while (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        groups.AddRange(Sudoku.ImportGroups(pathInput, possibleValues));
                        Console.WriteLine("Group path:");
                        pathInput = Console.ReadLine();
                    }
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
        public static ImmList<string> GetDomain(string path)
        {
            var values = new List<string>();
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    string line = r.ReadLine();
                    if (line != null)
                    {
                        values = line.ToCharArray().Select(v => v.ToString()).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Domain import error: {0}", ex.Message));
            }
            return values.ToImmList();
        }

        public static ImmMap<int, ImmList<string>> ImportSudoku(string path, ImmList<string> possibleValue, string undefined)
        {
            Dictionary<int, ImmList<string>> fields = new Dictionary<int, ImmList<string>>();
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    int row = 0;
                    string line;
                    r.ReadLine(); // skip first line (domain)
                    while ((line = r.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            SetRowFieldsOutOfStringLine(fields, line, row, possibleValue, undefined);
                            row++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Sudoku import error: {0}", ex.Message));
            }
            return fields.ToImmMap();
        }

        public static ImmList<ImmList<int>> ImportGroups(string path, ImmList<string> possibleValue)
        {
            var result = new List<ImmList<int>>();
            try
            {
                int row = 0;
                var groupMap = new Dictionary<string, ImmList<int>>();
                using (StreamReader r = File.OpenText(path))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        var values = GetGroupValuesOutOfLine(possibleValue, line, row);

                        foreach (var value in values)
                        {
                            if (groupMap.ContainsKey(value.Key))
                            {
                                groupMap[value.Key] = groupMap[value.Key].AddLastRange(value.Value.ToImmList());
                            }
                            else
                            {
                                groupMap.Add(value.Key, value.Value.ToImmList());
                            }
                        }

                        row++;
                    }
                }

                foreach (var key in groupMap.Keys)
                {
                    result.Add(groupMap[key]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }

            return result.ToImmList();
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
            if (value.Key != undefined)
            {
                return ImmList.Of(value.Key);
            }
            return field;
        }

        private static void SetRowFieldsOutOfStringLine(Dictionary<int, ImmList<string>> fields, string line, int row, ImmList<string> possibleValue, string undefined)
        {
            line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray().Select((v, i) => new Tuple<int, string>(i, v)).ToList().ForEach(i => fields.Add(i.Item1 + row * possibleValue.Length, i.Item2 == undefined ? possibleValue : ImmList.Of(i.Item2)));
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOutOfLine(ImmList<string> possibleValue, string line, int row)
        {
            var values = line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v)).ToImmList();
            if (values.Length != possibleValue.Length) return null;

            var index = row * possibleValue.Length;
            return GetGroupValuesOutOfValues(possibleValue, index + 1, row, values.RemoveFirst(), ImmMap.Of(new KeyValuePair<string, ImmList<int>>(values.First, ImmList.Of(index))));
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOutOfValues(ImmList<string> possibleValue, int index, int row, ImmList<string> values, ImmMap<string, ImmList<int>> groups)
        {
            if (index >= possibleValue.Length * (row + 1)) return groups;

            var value = values.First;
            if (!groups.ContainsKey(value)) return GetGroupValuesOutOfValues(possibleValue, index + 1, row, values.RemoveFirst(), groups.Add(value, ImmList.Of(index)));

            return GetGroupValuesOutOfValues(possibleValue, index + 1, row, values.RemoveFirst(), groups.Set(value, groups[value].AddLast(index)));
        }

        private static ImmMap<int, ImmList<int>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index)
        {
            if (fields[index].Length == 1) return GetBacktrackedSolvedSudoku(fields, groups, index + 1);

            return GetBacktrackedSolvedSudoku(fields, groups, index, fields[index]);
        }

        private static ImmMap<int, ImmList<int>> GetBacktrackedSolvedSudoku(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, ImmList<string> valuesToCheck)
        {
            var value = valuesToCheck.First;
            if (IsValueValid(fields, groups, index, value))
            {
                return GetBacktrackedSolvedSudoku(fields, groups, index + 1);
            }
            return GetBacktrackedSolvedSudoku(fields, groups, index, valuesToCheck.RemoveFirst());
        }

        private static bool IsValueValid(ImmMap<int, ImmList<string>> fields, ImmList<ImmList<int>> groups, int index, string value)
        {
            return groups.Where(g => g.Contains(index) && g.Length == 1).SelectMany(g => g).Distinct().Select(g => fields[g]).SelectMany(g => g).Distinct().Contains(value);
        }
    }
}