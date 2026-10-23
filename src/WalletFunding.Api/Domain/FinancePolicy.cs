namespace WalletFunding.Api;

// Server-owned product rules. The client may display or echo a rate, but only these values
// decide how much money is credited, so a request cannot choose its own interest or loan size.
public static class FinancePolicy
{
    public sealed record LoanProduct(decimal AnnualRate, decimal MaxAmount, int MaxTermMonths);

    public static readonly IReadOnlyDictionary<string, LoanProduct> Loans = new Dictionary<string, LoanProduct>(StringComparer.OrdinalIgnoreCase)
    {
        ["Vay tiêu dùng"] = new(18m, 500_000_000m, 60),
        ["Vay mua xe"] = new(15m, 1_000_000_000m, 84),
        ["Vay mua nhà"] = new(9m, 2_000_000_000m, 300),
        ["Vay kinh doanh"] = new(12m, 1_000_000_000m, 120),
    };

    public const decimal LoanMinAmount = 10_000_000m;
    public const int MaxActiveLoans = 3;

    // Total outstanding principal a wallet owner may hold across active loans.
    public static decimal LoanCeiling(string kycTier) => kycTier == "TIER_2" ? 2_000_000_000m : 100_000_000m;

    public const decimal SavingsMinAmount = 100_000m;
    public const int SavingsMaxTermMonths = 60;

    public sealed record SavingsTier(int FromTerm, int ToTerm, decimal AnnualRate);

    // Annual savings rate by term in months; term 0 is the flexible (no-term) product.
    // The client reads this table from GET /api/savings/products, so the UI shows the rate that is applied.
    public static readonly IReadOnlyList<SavingsTier> SavingsTiers =
    [
        new(0, 0, 4.2m),
        new(1, 2, 5.5m),
        new(3, 5, 6.0m),
        new(6, 11, 6.5m),
        new(12, SavingsMaxTermMonths, 7.0m),
    ];

    public static decimal SavingsRate(int termMonths) =>
        SavingsTiers.First(t => termMonths >= t.FromTerm && termMonths <= t.ToTerm).AnnualRate;

    public const string CryptoType = "Tiền điện tử";

    public static readonly IReadOnlyDictionary<string, string> CryptoSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["bitcoin"] = "BTCUSDT", ["btc"] = "BTCUSDT", ["ethereum"] = "ETHUSDT", ["eth"] = "ETHUSDT", ["bnb"] = "BNBUSDT",
        ["solana"] = "SOLUSDT", ["sol"] = "SOLUSDT", ["cardano"] = "ADAUSDT", ["ada"] = "ADAUSDT", ["xrp"] = "XRPUSDT",
        ["dogecoin"] = "DOGEUSDT", ["doge"] = "DOGEUSDT", ["polkadot"] = "DOTUSDT",
    };
}
