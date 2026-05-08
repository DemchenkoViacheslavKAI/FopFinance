namespace FopFinance.Contracts
{
    /// <summary>
    /// Уніфікована відповідь для JS-bridge.
    /// </summary>
    public sealed class BridgeResponse
    {
        public bool Ok { get; set; }
        public string Data { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
