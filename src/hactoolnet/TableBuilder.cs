using System;
using System.Collections.Generic;
using System.Text;

namespace hactoolnet
{
    public class TableBuilder
    {
        private List<string[]> Rows { get; } = new List<string[]>();
        private int ColumnCount { get; set; }

        public TableBuilder(params string[] header)
        {
            ColumnCount = header.Length;
            Rows.Add(header);
        }

        public TableBuilder(int columnCount)
        {
            ColumnCount = columnCount;
        }

        public void AddRow(params string[] row)
        {
            if (row.Length != ColumnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "All rows must have the same number of columns");
            }

            Rows.Add(row);
        }

        public string Print()
        {
            var sb = new StringBuilder();
            int[] width = new int[ColumnCount];

            foreach (string[] row in Rows)
            {
                for (int i = 0; i < ColumnCount - 1; i++)
                {
                    width[i] = Math.Max(width[i], row[i]?.Length ?? 0);
                }
            }

            foreach (string[] row in Rows)
            {
                for (int i = 0; i < ColumnCount; i++)
                {
                    sb.Append($"{(row[i] ?? string.Empty).PadRight(width[i] + 1, ' ')}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}