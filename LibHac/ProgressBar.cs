﻿// Adapted from https://gist.github.com/0ab6a96899cc5377bf54

using System;
using System.Text;
using System.Threading;

namespace LibHac
{
    public class ProgressBar : IDisposable, IProgressReport
    {
        private const int BlockCount = 20;
        private long _progress;
        private long _total;
        private readonly Timer _timer;

        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 30);
        private const string Animation = @"|/-\";

        private string _currentText = string.Empty;
        private bool _disposed;
        private int _animationIndex;

        private StringBuilder LogText { get; } = new StringBuilder();

        public ProgressBar()
        {
            var timerCallBack = new TimerCallback(TimerHandler);
            _timer = new Timer(timerCallBack, 0, 0, 0);
        }

        public void Report(long value)
        {
            Interlocked.Exchange(ref _progress, value);
        }

        public void ReportAdd(long value)
        {
            Interlocked.Add(ref _progress, value);
        }

        public void LogMessage(string message)
        {
            lock (_timer)
            {
                LogText.AppendLine(message);
            }
        }

        public void SetTotal(long value)
        {
            Interlocked.Exchange(ref _total, value);
            Report(0);
        }

        private void TimerHandler(object state)
        {
            lock (_timer)
            {
                if (_disposed) return;

                string text = string.Empty;

                if (_total > 0)
                {
                    double progress = _total == 0 ? 0 : (double)_progress / _total;
                    int progressBlockCount = (int)Math.Min(progress * BlockCount, BlockCount);
                    text = $"[{new string('#', progressBlockCount)}{new string('-', BlockCount - progressBlockCount)}] {_progress}/{_total} {progress:P1} {Animation[_animationIndex++ % Animation.Length]}";
                }
                UpdateText(text);

                ResetTimer();
            }
        }

        private void UpdateText(string text)
        {
            var outputBuilder = new StringBuilder();

            if (LogText.Length > 0)
            {
                // Erase current text
                outputBuilder.Append("\r");
                outputBuilder.Append(' ', _currentText.Length);
                outputBuilder.Append("\r");
                outputBuilder.Append(LogText);
                _currentText = string.Empty;
                LogText.Clear();
            }

            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(_currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == _currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            outputBuilder.Append('\b', _currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = _currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            _currentText = text;
        }

        private void ResetTimer()
        {
            _timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (_timer)
            {
                _disposed = true;
                UpdateText(string.Empty);
            }
        }
    }
}
