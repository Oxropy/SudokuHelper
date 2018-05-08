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
                var sudokuValues = Sudoku.ImportSudoku(pathInput, possibleValues);

                if (sudokuValues.Length > 0)
                {
                    Sudoku.PrintSudoku(sudokuValues, possibleValues.Length);

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    var groups = Sudoku.ImportGroups(pathInput, possibleValues);
                    Stopwatch sw = Stopwatch.StartNew();
                    var solved = Sudoku.Solve(groups, sudokuValues, possibleValues);
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
        public static ImmList<int> GetDomain(string path)
        {
            var values = new List<int>();
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    string line = r.ReadLine();
                    if (line != null)
                    {
                        values = line.Split(' ').Where(v => int.TryParse(v, out int val)).Select(v => Convert.ToInt32(v)).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Domain import error: {0}", ex.Message));
            }
            return values.ToImmList();
        }

        public static ImmMap<int, ImmList<int>> ImportSudoku(string path, ImmList<int> possibleValue)
        {
            Dictionary<int, ImmList<int>> fields = new Dictionary<int, ImmList<int>>();
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
                            SetRowFieldsOutOfStringLine(fields, line, row, possibleValue);
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

        public static ImmList<ImmList<int>> ImportGroups(string path, ImmList<int> possibleValue)
        {
            var result = new List<ImmList<int>>();
            try
            {
                int row = 0;
                var groupMap = new Dictionary<int, ImmList<int>>();
                using (StreamReader r = File.OpenText(path))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        var values = GetGroupValuesOutOfLine(possibleValue, line, row);

                        //foreach (var value in values)
                        //{
                        //    if (int.TryParse(value.Key, out int val))
                        //    {
                        //        if (groupMap.ContainsKey(val))
                        //        {

                        //        }
                        //        else
                        //        {
                        //            groupMap.Add(val, value.Value.ToImmList());
                        //        }
                        //    }
                        //}

                        row++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }

            return result.ToImmList();
        }

        public static ImmMap<int, ImmList<int>> Solve(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<int>> fields, ImmList<int> possibleValue)
        {
            return SolveField(groups, fields, possibleValue, 0);
        }

        public static void PrintSudoku(ImmMap<int, ImmList<int>> fields, int valueCount)
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

        private static void AppendField(StringBuilder sb, ImmList<int> field)
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

        private static ImmMap<int, ImmList<int>> SolveField(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<int>> fields, ImmList<int> possibleValue, int i)
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
                        return SolveField(groups, resultFixFields, possibleValue, i + 1);
                    }

                    var resultUniqueField = GetUniqueValue(groupFields.Select(g => fields[g]).ToImmList(), resultFixField);
                    var resultUniqueFields = fields.Remove(i).Add(i, resultUniqueField);
                    return SolveField(groups, resultUniqueFields, possibleValue, i + 1);
                }
                return SolveField(groups, fields, possibleValue, i + 1);
            }
            if (fields.Any(f => f.Value.Length > 1))
            {
                PrintSudoku(fields, possibleValue.Length);
                return SolveField(groups, fields, possibleValue, 0);
            }
            return fields;
        }

        private static ImmList<int> RemoveFixValuesFromPossibleValues(ImmList<int> values, ImmList<int> fixValues)
        {
            return values.Where(v => !fixValues.Contains(v)).ToImmList();
        }

        private static ImmList<int> GetUniqueValue(ImmList<ImmList<int>> groupValues, ImmList<int> field)
        {
            var value = groupValues.Where(g => g.Length != 1).SelectMany(g => g).ToList().GroupBy(g => g).ToDictionary(v => v.Key, v => v.ToList().Count).Where(v => v.Value == 1 && field.Contains(v.Key)).FirstOrDefault();
            if (value.Key != 0)
            {
                return ImmList.Of(value.Key);
            }
            return field;
        }

        //private static ImmList<int> RemoveValuesCauseThiesAreInAGroupOfSameValues(ImmList<int> groupFields, ImmMap<int, ImmList<int>> fields)
        //{
        //    var possibleValues = fields.Where(f => groupFields.Contains(f.Key) && f.Value.Length != 1).SelectMany(f => f.Value);

        //}

        private static void SetRowFieldsOutOfStringLine(Dictionary<int, ImmList<int>> fields, string line, int row, ImmList<int> possibleValue)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (int.TryParse(line[i].ToString(), out int value))
                {
                    fields.Add(row * possibleValue.Length + i, value == 0 ? possibleValue : ImmList.Of(value));
                }
            }
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOutOfLine(ImmList<int> possibleValue, string line, int row)
        {
            var values = line.Split(' ').ToImmList();
            if (values.Length != possibleValue.Length) return null;

            var index = row * possibleValue.Length;
            return GetGroupValuesOutOfValues(possibleValue, index + 1, values.RemoveFirst(), ImmMap.Of(new KeyValuePair<string, ImmList<int>>(values.First, ImmList.Of(index))));
        }

        private static ImmMap<string, ImmList<int>> GetGroupValuesOutOfValues(ImmList<int> possibleValue, int index, ImmList<string> values, ImmMap<string, ImmList<int>> groups)
        {
            if (index >= possibleValue.Length) return groups;

            var value = values.First;
            if (!groups.ContainsKey(value)) return GetGroupValuesOutOfValues(possibleValue, index + 1, values.RemoveFirst(), groups.Add(value, ImmList.Of(index)));

            return GetGroupValuesOutOfValues(possibleValue, index + 1, values.RemoveFirst(), groups.Set(value, groups[value].AddLast(index))); 
        }
    }
}