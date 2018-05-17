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

            var undefined = values.Item1;
            var possibleValues = values.Item2;
            var sudokuValues = values.Item3;

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
            Console.ReadKey();
        }
    }

    static class Sudoku
    {
        public static Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>> ImportDomainAndSudoku(string path)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var domain = GetUndefinedAndPossibleValues(r);
                    r.ReadLine(); // empty line
                    var sudoku = GetSudoku(r, domain.Item1, domain.Item2);

                    return new Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>(domain.Item1, domain.Item2, sudoku);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("´Sudoku import error: {0}", ex.Message));
            }
            return new Tuple<string, ImmList<string>, ImmMap<int, ImmList<string>>>(string.Empty, ImmList.Of(string.Empty), ImmMap.Of(new KeyValuePair<int, ImmList<string>>(0, ImmList.Of(string.Empty))));
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
            if (value.Key != undefined) return ImmList.Of(value.Key);
            return field;
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

        private static ImmMap<int, ImmList<string>> GetSudoku(StreamReader reader, string undefined, ImmList<string> possibleValues)
        {
            string line = reader.ReadLine();
            if (line == null) return ImmMap.Of(new KeyValuePair<int, ImmList<string>>(0, ImmList.Of(string.Empty)));

            int row = 0;
            return GetSudoku(reader, undefined, possibleValues, ImmMap.Of(SetRowFieldsOutOfStringLine(line, undefined, possibleValues, row)), row + 1);
        }

        private static ImmMap<int, ImmList<string>> GetSudoku(StreamReader reader, string undefined, ImmList<string> possibleValues, ImmMap<int, ImmList<string>> sudoku, int row)
        {
            string line = reader.ReadLine();
            if (line == null) return sudoku;

            return GetSudoku(reader, undefined, possibleValues, sudoku.AddRange(SetRowFieldsOutOfStringLine(line, undefined, possibleValues, row)), row + 1);
        }

        private static KeyValuePair<int, ImmList<string>>[] SetRowFieldsOutOfStringLine(string line, string undefined, ImmList<string> possibleValue, int row)
        {
            return line.Split(' ').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray().Select((v, i) => new Tuple<int, string>(i, v)).Select(v => new KeyValuePair<int, ImmList<string>>(v.Item1 + row * possibleValue.Length, v.Item2 == undefined ? possibleValue : ImmList.Of(v.Item2))).ToArray();
        }
    }
}