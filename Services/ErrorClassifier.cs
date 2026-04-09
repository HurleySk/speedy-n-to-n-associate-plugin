using Microsoft.Xrm.Sdk;
using System;
using System.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    internal static class ErrorClassifier
    {
        private const int MaxExceptionMessageLength = 200;
        private const string DuplicateKeyError = "Cannot insert duplicate key";

        private static readonly string[] TransientPatterns =
        {
            "429", "503", "502", "504",
            "throttl", "server busy", "try again",
            "timeout", "timed out", "task was canceled",
            "connection was closed", "connection reset", "underlying connection",
            "error occurred while sending", "socket", "network"
        };

        public static string GetExceptionSummary(Exception ex)
        {
            var msg = ex.Message;
            return msg.Length > MaxExceptionMessageLength
                ? msg.Substring(0, MaxExceptionMessageLength) + "..."
                : msg;
        }

        public static bool IsTransient(Exception ex)
        {
            return IsTransientMessage(ex.Message) ||
                   (ex.InnerException != null && IsTransientMessage(ex.InnerException.Message));
        }

        public static bool IsTransientFault(OrganizationServiceFault fault)
        {
            if (fault == null) return false;
            var msg = fault.Message ?? "";
            return TransientPatterns.Any(p => msg.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsDuplicateKey(Exception ex)
        {
            return ex.Message.IndexOf(DuplicateKeyError, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsDuplicateKeyFault(OrganizationServiceFault fault)
        {
            if (fault == null) return false;
            var msg = fault.Message ?? "";
            return msg.IndexOf(DuplicateKeyError, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsConnectionError(Exception ex)
        {
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            return msg.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   msg.IndexOf("socket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   msg.IndexOf("underlying", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTransientMessage(string msg)
        {
            return !string.IsNullOrEmpty(msg) &&
                   TransientPatterns.Any(p => msg.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
