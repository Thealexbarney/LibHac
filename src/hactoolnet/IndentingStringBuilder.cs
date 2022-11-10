using System;
using System.Text;

namespace hactoolnet;

public struct ScopedIndentation : IDisposable
{
    private IndentingStringBuilder _builder;

    public ScopedIndentation(IndentingStringBuilder builder)
    {
        builder.IncreaseLevel();
        _builder = builder;
    }

    public void Dispose()
    {
        _builder.DecreaseLevel();
    }
}

public class IndentingStringBuilder
{
    public int LevelSize { get; set; } = 4;
    public int Level { get; private set; }

    private StringBuilder _sb = new StringBuilder();
    private string _indentation = string.Empty;
    private bool _hasIndentedCurrentLine;
    private bool _lastLineWasEmpty;
    private int _columnLength;
    private int _paddingSize;

    public IndentingStringBuilder(int columnLength)
    {
        _columnLength = columnLength;
        _paddingSize = columnLength;
    }

    public IndentingStringBuilder(int levelSize, int columnLength)
    {
        LevelSize = levelSize;
        _columnLength = columnLength;
        _paddingSize = columnLength;
    }

    public void SetLevel(int level)
    {
        Level = Math.Max(level, 0);
        _indentation = new string(' ', Level * LevelSize);
        _paddingSize = _columnLength - Level * LevelSize;
    }

    public void IncreaseLevel() => SetLevel(Level + 1);
    public void DecreaseLevel() => SetLevel(Level - 1);

    public IndentingStringBuilder AppendLine()
    {
        _sb.AppendLine();
        _hasIndentedCurrentLine = false;
        _lastLineWasEmpty = true;
        return this;
    }

    public IndentingStringBuilder AppendSpacerLine()
    {
        if (!_lastLineWasEmpty)
        {
            _sb.AppendLine();
            _hasIndentedCurrentLine = false;
            _lastLineWasEmpty = true;
        }

        return this;
    }

    public IndentingStringBuilder AppendLine(string value)
    {
        IndentIfNeeded();
        _sb.AppendLine(value);
        _hasIndentedCurrentLine = false;
        _lastLineWasEmpty = string.IsNullOrWhiteSpace(value);
        return this;
    }

    public IndentingStringBuilder Append(string value)
    {
        IndentIfNeeded();
        _sb.Append(value);
        return this;
    }

    public IndentingStringBuilder Append(char value, int repeatCount)
    {
        IndentIfNeeded();
        _sb.Append(value, repeatCount);
        return this;
    }

    public ScopedIndentation AppendHeader(string value)
    {
        AppendLine(value);
        return new ScopedIndentation(this);
    }

    public void PrintItem(string prefix, object data)
    {
        if (data is byte[] byteData)
        {
            AppendBytes(prefix.PadRight(_paddingSize), byteData);
        }
        else
        {
            AppendLine(prefix.PadRight(_paddingSize) + data);
        }
    }

    public IndentingStringBuilder AppendLineAndIncrease(string value)
    {
        AppendLine(value);
        IncreaseLevel();
        return this;
    }

    public IndentingStringBuilder DecreaseAndAppendLine(string value)
    {
        DecreaseLevel();
        AppendLine(value);
        return this;
    }

    private void IndentIfNeeded()
    {
        if (!_hasIndentedCurrentLine)
        {
            _sb.Append(_indentation);
            _hasIndentedCurrentLine = true;
        }
    }

    public void AppendBytes(string prefix, byte[] data)
    {
        int max = 32;
        int remaining = data.Length;
        bool first = true;
        int offset = 0;

        while (remaining > 0)
        {
            max = Math.Min(max, remaining);

            if (first)
            {
                Append(prefix);
                first = false;
            }
            else
            {
                Append(' ', prefix.Length);
            }

            for (int i = 0; i < max; i++)
            {
                Append($"{data[offset++]:X2}");
            }

            AppendLine();
            remaining -= max;
        }
    }

    public override string ToString() => _sb.ToString();
}