namespace SeriMongo.Services
{
    internal static class OtlpSeverity
    {
        public static string FromNumber(int number)
        {
            return number switch
            {
                >= 21 => "Fatal",
                >= 17 => "Error",
                >= 13 => "Warning",
                >= 9 => "Information",
                >= 5 => "Debug",
                _ => "Trace"
            };
        }

        public static string NormalizeText(string severity)
        {
            switch ((severity ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "TRACE":
                case "TRACE1":
                case "TRACE2":
                case "TRACE3":
                case "TRACE4":
                    return "Trace";
                case "DEBUG":
                case "DEBUG1":
                case "DEBUG2":
                case "DEBUG3":
                case "DEBUG4":
                    return "Debug";
                case "INFO":
                case "INFORMATION":
                case "INFO1":
                case "INFO2":
                case "INFO3":
                case "INFO4":
                    return "Information";
                case "WARN":
                case "WARNING":
                case "WARN1":
                case "WARN2":
                case "WARN3":
                case "WARN4":
                    return "Warning";
                case "ERROR":
                case "ERROR1":
                case "ERROR2":
                case "ERROR3":
                case "ERROR4":
                    return "Error";
                case "FATAL":
                case "CRITICAL":
                case "FATAL1":
                case "FATAL2":
                case "FATAL3":
                case "FATAL4":
                    return "Fatal";
                default:
                    return severity;
            }
        }
    }
}
