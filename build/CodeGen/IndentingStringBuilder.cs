using System;
using System.Text;

namespace LibHacBuild.CodeGen;

public class IndentingStringBuilder
{
    public int LevelSize { get; set; } = 4;
    public int Level { get; private set; }

    private StringBuilder _sb = new StringBuilder();
    private string _indentation = string.Empty;
    private bool _hasIndentedCurrentLine;
    private bool _lastLineWasEmpty;

    public IndentingStringBuilder() { }
    public IndentingStringBuilder(int levelSize) => LevelSize = levelSize;

    public void SetLevel(int level)
    {
        Level = Math.Max(level, 0);
        _indentation = new string(' ', Level * LevelSize);
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

    public IndentingStringBuilder DecreaseAndAppend(string value)
    {
        DecreaseLevel();
        Append(value);
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

    public override string ToString() => _sb.ToString();
}