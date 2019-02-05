namespace LibHac
{
    public interface IProgressReport
    {
        /// <summary>
        /// Sets the current value of the <see cref="IProgressReport"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to set.</param>
        void Report(long value);

        /// <summary>
        /// Adds <paramref name="value"/> to the current value of the <see cref="IProgressReport"/>.
        /// </summary>
        /// <param name="value">The amount to add.</param>
        void ReportAdd(long value);

        /// <summary>
        /// Sets the maximum value of the <see cref="IProgressReport"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The maximum value to set.</param>
        void SetTotal(long value);

        /// <summary>
        /// Logs a message to the <see cref="IProgressReport"/> object.
        /// </summary>
        /// <param name="message">The message to output.</param>
        void LogMessage(string message);
    }
}