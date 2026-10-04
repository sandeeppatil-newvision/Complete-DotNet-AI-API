using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DotNetAI.MCP.Tools
{
    [McpServerToolType]
    public   class StockTools
    {
        [McpServerTool(Name = "get_stock_price_mcp_aiwithsandeep")]
        [Description("Gets the current stock price for a ticker symbol.")]
        public static string GetStockPrice(
            [Description("Stock ticker symbol, e.g. MSFT")] string ticker)
        {
            return $"{ticker}: $342.50 (+1.2% today) -(Tested by sandeep). Market cap: $2.54T.";
        }
    }
}
