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
            const string undefined = "_";
            Console.WriteLine("Sudoku path:");
            string pathInput = Console.ReadLine();
            var values = SudokuImport.ImportDomainAndSudoku(pathInput, undefined);

            if (values.IsSome)
            {
                var possibleValues = values.Value.Item1;
                var sudokuValues = values.Value.Item2;

                if (sudokuValues.Length > 0)
                {
                    SudokuPrinter.PrintSudoku(sudokuValues, possibleValues.Length);

                    var groups = new List<ImmList<int>>();

                    Console.WriteLine("Group path:");
                    pathInput = Console.ReadLine();
                    while (!string.IsNullOrWhiteSpace(pathInput))
                    {
                        var group = SudokuImport.ImportGroups(pathInput, undefined, possibleValues.Length);
                        if (group.IsSome)
                        {
                            groups.AddRange(group.Value);
                        }
                        Console.WriteLine("Group path:");
                        pathInput = Console.ReadLine();
                    }

                    if (groups.Count > 0)
                    {
                        SudokuPrinter.PrintGroups(groups.ToImmList(), possibleValues.Length);

                        Stopwatch sw = Stopwatch.StartNew();
                        var solved = SudokuSolver.Solve(groups.ToImmList(), sudokuValues);
                        sw.Stop();
                        Console.WriteLine("Time: {0}ms", sw.ElapsedMilliseconds);

                        if (solved.IsSome) SudokuPrinter.PrintSudoku(solved.Value, possibleValues.Length);
                        else Console.WriteLine("Unsolved!");
                    }
                }
            }
            Console.ReadKey();
        }
    }

    static class SudokuSolver
    {
        public static Optional<ImmMap<int, ImmList<string>>> Solve(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields)
        {
            return GetBacktrackedSolvedSudokuNoneTest(groups, fields, 0, Optional.None);
        }

        private static Optional<ImmMap<int, ImmList<string>>> GetBacktrackedSolvedSudokuNoneTest(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, int index, Optional<ImmList<string>> checkValues)
        {
            if (!fields.ContainsKey(index)) return fields;
            if (checkValues.IsSome && checkValues.Value.Length == 0) return Optional.None;
            if (checkValues.IsNone && fields[index].Length > 1) return GetBacktrackedSolvedSudoku(groups, fields, index, fields[index]);
            var newFields = GetBacktrackedSolvedSudoku(groups, fields, index, checkValues);
            if (newFields.IsNone && checkValues.IsNone) return Optional.None;
            if (newFields.IsNone && checkValues.IsSome) return GetBacktrackedSolvedSudokuNoneTest(groups, fields, index, checkValues.Value.RemoveFirst());
            return newFields;
        }

        private static Optional<ImmMap<int, ImmList<string>>> GetBacktrackedSolvedSudoku(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, int index, Optional<ImmList<string>> checkValues)
        {
            if (checkValues.IsNone) return GetBacktrackedSolvedSudokuNoneTest(groups, fields, index + 1, Optional.None);

            var fieldsWithNewCheckValue = fields.Set(index, ImmList.Of(checkValues.Value.First));
            if (IsValueValid(groups, fieldsWithNewCheckValue, index)) return GetBacktrackedSolvedSudokuNoneTest(groups, fieldsWithNewCheckValue, index + 1, Optional.None);
            if (checkValues.Value.TryFirst.IsNone) return Optional.None;
            return GetBacktrackedSolvedSudokuNoneTest(groups, fields, index, checkValues.Value.RemoveFirst());
        }

        private static bool IsValueValid(ImmList<ImmList<int>> groups, ImmMap<int, ImmList<string>> fields, int index)
        {
            return groups.Where(g => g.Contains(index)) // groups which contains this field
                .SelectMany(g => g)
                .Distinct() // distinct same index
                .Select(g => fields[g]) // get real field value
                .Where(g => g.Length == 1) // remove unsolved fields
                .SelectMany(g => g)
                .Where(g => g == fields[index].First) // find value to valid
                .Count() == 1; // value is unique
        }
    }

    static class SudokuImport
    {
        public static Optional<Tuple<ImmList<string>, ImmMap<int, ImmList<string>>>> ImportDomainAndSudoku(string path, string undefined)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var replacedSplit = r.ReadToEnd().Replace("\r\n", "\n").Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToImmList();
                    var possibleValues = replacedSplit.First.Select(c => c.ToString()).ToImmList();
                    var fields = replacedSplit.RemoveFirst().Select((v, i) => new KeyValuePair<int, ImmList<string>>(i, v == undefined ? possibleValues : ImmList.Of(v))).ToImmMap();

                    if (fields.Where(f => f.Value.Length == 1).Any(f => f.Value.First != undefined && !possibleValues.Contains(f.Value.First))) return Optional.None;

                    return new Tuple<ImmList<string>, ImmMap<int, ImmList<string>>>(possibleValues, fields);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("´Sudoku import error: {0}", ex.Message));
            }
            return Optional.None;
        }

        public static Optional<ImmList<ImmList<int>>> ImportGroups(string path, string undefined, int length)
        {
            try
            {
                using (StreamReader r = File.OpenText(path))
                {
                    var file = r.ReadToEnd().Replace("\r\n", "\n").Replace("\r", "\n");
                    var groups = Regex.Split(file, "\\n\\n", RegexOptions.IgnorePatternWhitespace)
                        .Select(g => g.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(g => g.Select((v, i) => new Tuple<string, int>(v, i))
                        .Where(v => v.Item1 != undefined)
                        .GroupBy(i => i.Item1, i => i.Item2, (k, v) => new KeyValuePair<string, ImmList<int>>(k, v.ToImmList()))
                        .ToImmMap()).SelectMany(g => g.Values).ToImmList();

                    if (groups.Any(g => g.Length != length)) return Optional.None;

                    return groups;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Group import error: {0}", ex.Message));
            }
            return Optional.None;
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
            Console.WriteLine();
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