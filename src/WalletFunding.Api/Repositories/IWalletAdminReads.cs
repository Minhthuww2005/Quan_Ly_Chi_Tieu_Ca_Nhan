using System.Text.Json.Nodes;

namespace WalletFunding.Api;

// Read models for the admin pages, computed by the database (aggregates, joins, LIMIT/OFFSET)
// instead of loading users, wallets, transactions and KYC images into memory on every request.
public interface IWalletAdminReads
{
    Task<JsonObject> AdminStats(string today);
    // Each row is the user document plus _balance, _transaction_count and _kyc_status.
    Task<(List<JsonObject> Rows, int Total)> AdminUsers(int offset, int limit);
    // Each row is the transaction document plus _sender_name; Stats covers every transaction.
    Task<(List<JsonObject> Rows, int Total, JsonObject Stats)> AdminTransactions(int offset, int limit, string today);
    // KYC documents without the base64 images, plus _user_name, _user_email and _has_images.
    Task<List<JsonObject>> AdminKyc(bool pendingOnly);
}
