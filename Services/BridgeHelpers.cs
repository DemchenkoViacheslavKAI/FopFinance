using System.Text.Json;
using FopFinance.Contracts;

namespace FopFinance.Services
{
    /// <summary>
    /// Shared JSON serialisation options and response helpers for all bridge services.
    /// </summary>
    internal static class BridgeHelpers
    {
        internal static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented        = false
        };

        internal static readonly JsonSerializerOptions ReadOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        internal static string Ok(string data = "") =>
            JsonSerializer.Serialize(new BridgeResponse { Ok = true, Data = data }, JsonOpts);

        internal static string Error(string msg) =>
            JsonSerializer.Serialize(new BridgeResponse { Ok = false, Error = msg }, JsonOpts);

        internal static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, ReadOpts);
    }
}
