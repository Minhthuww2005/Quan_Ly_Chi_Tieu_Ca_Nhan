using System.Text.Json.Nodes;

namespace WalletFunding.Api;

// Encapsulates balance rules while preserving the imported JSONB representation.
public sealed class WalletAccount(JsonObject document)
{
    public int Id => J.Int(document, "wallet_id");
    public int OwnerId => J.Int(document, "user_id");
    public decimal Balance => J.Money(document, "balance");

    public void Debit(decimal amount)
    {
        EnsureActive();
        EnsureAmount(amount);
        if (Balance < amount) throw new ApiError(400, "Số dư không đủ");
        document["balance"] = Balance - amount;
    }

    public void Credit(decimal amount)
    {
        EnsureActive();
        EnsureAmount(amount);
        document["balance"] = Balance + amount;
    }

    private void EnsureActive()
    {
        if (J.Str(document, "status", "active") != "active") throw new ApiError(403, "Ví không hoạt động");
    }

    private static void EnsureAmount(decimal amount)
    {
        if (amount < 0 || decimal.Truncate(amount) != amount) throw new ApiError(400, "Số tiền không hợp lệ");
    }
}
