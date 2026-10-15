using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace WalletFunding.Api;

public sealed class AdministrationService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        await Prepare(context);
        var data = Area == "kyc" ? await Kyc() : await Administration();
        return new ServiceResponse(data, StatusCode);
    }

    private async Task<JsonObject> Administration()
    {
        Require(Admin, "Yêu cầu quyền quản trị", 403);
        var users = await store.All("users"); var wallets = await store.All("wallets"); var txs = await store.All("transactions"); var kyc = await store.All("kycprofiles");
        if (Action == "stats" && Method == "GET")
            return J.Obj(new { success = true, stats = new { totalUsers = users.Count, activeUsers = users.Count(x => !Blocked(x)), totalWallets = wallets.Count, totalBalance = wallets.Sum(x => J.Money(x, "balance")), totalTransactions = txs.Count, pendingKYC = kyc.Count(x => J.Str(x, "status") is "submitted" or "pending"), verifiedKYC = kyc.Count(x => J.Str(x, "status") == "verified"), totalCards = (await store.All("cards")).Count, totalBankAccounts = (await store.All("bankaccounts")).Count } });
        if (Action is "users" or "user")
        {
            if (parts.Length > 2)
            {
                Require(int.TryParse(parts[2], out var id), "Mã người dùng không hợp lệ"); var user = await User(id);
                if (Method == "GET") return J.Obj(new { success = true, user = J.Public(user), wallet = wallets.FirstOrDefault(x => J.Int(x, "user_id") == id), kyc = kyc.Where(x => J.Int(x, "user_id") == id).Select(x => J.Public(x)).ToArray() });
                Require(id != UserId, "Không thể khóa hoặc xóa tài khoản đang đăng nhập");
                if (Method == "DELETE") { user["status"] = "inactive"; user["accountStatus"] = "closed"; user["deleted_at"] = Now; }
                else if (Method is "PUT" or "PATCH")
                {
                    if (body.ContainsKey("status")) { var status = J.Str(body, "status"); Require(status is "active" or "inactive" or "suspended" or "blocked", "Trạng thái không hợp lệ"); user["status"] = status; user["accountStatus"] = status == "suspended" ? "blocked" : status; }
                    if (Method == "PUT") foreach (var f in new[] { "name", "email", "phone" }) if (body.ContainsKey(f)) { Require(J.Str(body, f).Length > 0, "Thiếu thông tin: " + f); user[f] = body[f]!.DeepClone(); }
                    user["updated_at"] = Now;
                }
                else throw new ApiError(404, "Endpoint không tồn tại");
                return J.Obj(new { success = true, message = "Cập nhật tài khoản thành công", user = J.Public(user) });
            }
            var list = users.Select(x =>
            {
                var d = J.Public(x); var wallet = wallets.FirstOrDefault(w => J.Int(w, "user_id") == J.Int(x, "user_id"));
                d["balance"] = wallet == null ? 0 : J.Money(wallet, "balance"); d["transaction_count"] = wallet == null ? 0 : txs.Count(t => J.Int(t, "wallet_id") == J.Int(wallet, "wallet_id"));
                var status = kyc.LastOrDefault(k => J.Int(k, "user_id") == J.Int(x, "user_id")); d["kyc_status"] = status == null ? "none" : J.Str(status, "status") == "submitted" ? "pending" : J.Str(status, "status"); return d;
            }).OrderByDescending(x => J.Str(x, "created_at")).ToArray();
            return Paginate(list, "users");
        }
        if (Action == "transactions" && Method == "GET")
        {
            var list = txs.OrderByDescending(x => J.Str(x, "timestamp")).Select(x =>
            {
                var d = J.Public(x); var wallet = wallets.FirstOrDefault(w => J.Int(w, "wallet_id") == J.Int(x, "wallet_id")); var user = users.FirstOrDefault(u => J.Int(u, "user_id") == (wallet == null ? -1 : J.Int(wallet, "user_id")));
                d["transaction_id"] = J.Str(x, "trans_id"); d["sender_name"] = user == null ? "Không xác định" : J.Str(user, "name"); d["recipient_name"] = J.Str(x, "recipient", J.Str(x, "description")); d["created_at"] = J.Str(x, "timestamp"); d["status"] = J.Str(x, "status") == "success" ? "completed" : J.Str(x, "status"); d["fee"] ??= JsonValue.Create(0); return d;
            }).ToArray();
            var result = Paginate(list, "transactions"); var completed = list.Where(x => J.Str(x, "status") == "completed").ToArray();
            result["stats"] = J.Obj(new { total = list.Length, completed = completed.Length, pending = list.Count(x => J.Str(x, "status") == "pending"), failed = list.Count(x => J.Str(x, "status") == "failed"), totalVolume = completed.Where(x => J.Money(x, "amount") < 0).Sum(x => Math.Abs(J.Money(x, "amount"))), totalFees = completed.Sum(x => J.Money(x, "fee")), todayTransactions = list.Count(x => J.Str(x, "timestamp").StartsWith(Today)), todayVolume = completed.Where(x => J.Str(x, "timestamp").StartsWith(Today) && J.Money(x, "amount") < 0).Sum(x => Math.Abs(J.Money(x, "amount"))) }); return result;
        }
        if (Action == "kyc")
        {
            if (Method == "GET")
            {
                var list = kyc.Where(x => parts.ElementAtOrDefault(2) != "pending" || J.Str(x, "status") is "pending" or "submitted").Select(x =>
                {
                    var d = J.Public(x); var user = users.FirstOrDefault(u => J.Int(u, "user_id") == J.Int(x, "user_id"));
                    d["verification_id"] = J.Str(x, "kyc_id"); d["user_name"] = user == null ? "" : J.Str(user, "name"); d["user_email"] = user == null ? J.Str(x, "email") : J.Str(user, "email");
                    d["id_number"] = J.Str(x, "national_id_masked", "Đã mã hóa"); d["date_of_birth"] = "Đã mã hóa"; d["status"] = J.Str(x, "status") == "submitted" ? "pending" : J.Str(x, "status");
                    foreach (var f in new[] { "front_image", "back_image", "face_image" }) d[f] ??= JsonValue.Create(""); return d;
                }).ToArray(); return J.Obj(new { success = true, kyc_requests = list, requests = list });
            }
            var operation = parts.ElementAtOrDefault(2) is "approve" or "reject" ? parts[2] : parts.ElementAtOrDefault(3);
            var key = parts.ElementAtOrDefault(2) is "approve" or "reject" ? parts.ElementAtOrDefault(3) : parts.ElementAtOrDefault(2);
            Require(operation is "approve" or "reject", "Endpoint không tồn tại", 404);
            var profile = await Find("kycprofiles", "kyc_id", key ?? ""); Require(J.Str(profile, "status") is "submitted" or "pending", "Hồ sơ đã được xử lý", 409);
            var userKyc = await User(J.Int(profile, "user_id"));
            if (operation == "approve") { profile["status"] = "verified"; profile["verification_level"] = 2; profile["verified_at"] = Now; userKyc["kycTier"] = "TIER_2"; if (J.Str(profile, "photo_review_status") == "pending") profile["photo_review_status"] = "approved"; }
            else { Required("reason"); profile["status"] = "rejected"; profile["rejection_reason"] = J.Str(body, "reason"); userKyc["kycTier"] = "TIER_1"; }
            profile["updated_at"] = Now; await Notify(J.Int(profile, "user_id"), "KYC", operation == "approve" ? "Hồ sơ đã được duyệt" : "Hồ sơ bị từ chối: " + J.Str(body, "reason"));
            return J.Obj(new { success = true, message = "Đã xử lý hồ sơ KYC", kyc = J.Public(profile) });
        }
        throw new ApiError(404, "Endpoint không tồn tại");
    }

    private async Task<JsonObject> Kyc()
    {
        if (Method == "GET")
        {
            var key = Action == "status" ? parts.ElementAtOrDefault(2) : Action; Require(int.TryParse(key, out var id), "Mã người dùng không hợp lệ"); Own(id);
            var profile = (await store.All("kycprofiles")).LastOrDefault(x => J.Int(x, "user_id") == id) ?? throw new ApiError(404, "Chưa có thông tin KYC");
            return J.Obj(new { success = true, kyc = J.Public(profile), data = J.Public(profile) });
        }
        if (Method == "PATCH" && Action == "update-biometric")
        {
            Require(int.TryParse(parts.ElementAtOrDefault(2), out var id), "Mã người dùng không hợp lệ"); Own(id);
            var profile = await Owned("kycprofiles", "user_id", id.ToString());
            Require(J.Str(profile, "status") == "verified", "Chỉ bổ sung ảnh sau khi hồ sơ được duyệt", 409);
            foreach (var field in new[] { "front_image", "back_image", "face_image" })
            {
                var image = J.Str(body, field);
                Require(image.Length is > 30 and < 2_800_000 && System.Text.RegularExpressions.Regex.IsMatch(image, @"^data:image/(jpeg|png|webp);base64,[A-Za-z0-9+/=]+$"), "Ảnh xác thực không hợp lệ hoặc vượt 2 MB");
                profile[field] = image;
            }
            profile["has_biometric"] = false; profile["photo_review_status"] = "pending";
            profile["status"] = "submitted"; profile["updated_at"] = Now;
            (await User(id))["kycTier"] = "TIER_1";
            return J.Obj(new { success = true, kyc = J.Public(profile) });
        }
        Require(Method == "POST" && Action is "submit" or "submit-verification", "Endpoint không tồn tại", 404);
        var national = J.Str(body, "national_id", J.Str(body, "id_number"));
        Required("full_name"); Require(System.Text.RegularExpressions.Regex.IsMatch(national, "^[0-9]{9,12}$"), "CMND/CCCD không hợp lệ");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(national))).ToLowerInvariant();
        var profiles = await store.All("kycprofiles");
        Require(!profiles.Any(x => J.Str(x, "national_id_hash") == hash && J.Int(x, "user_id") != RequestedId), "CMND/CCCD đã được sử dụng", 409);
        Require(!profiles.Any(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "status") is "pending" or "submitted" or "verified"), "Đã có hồ sơ đang chờ hoặc được duyệt", 409);
        var profileNew = J.Obj(new { kyc_id = await store.Next("kycprofiles", "kyc_id"), user_id = RequestedId, full_name = J.Str(body, "full_name"), email = J.Str(body, "email", J.Str(current!, "email")), national_id_hash = hash, national_id_encrypted = Encrypt(national), national_id_masked = new string('*', national.Length - 4) + national[^4..], status = "submitted", verification_level = 1, has_biometric = false, aml_check = false, sanctions_check = false, pep_check = false, created_at = Now, updated_at = Now, submitted_at = Now });
        foreach (var f in new[] { "date_of_birth", "phone", "address", "document_number" }) profileNew[f + "_encrypted"] = Encrypt(J.Str(body, f));
        foreach (var f in new[] { "gender", "nationality", "city", "district", "document_type", "document_issue_date", "document_issue_place", "front_image", "back_image", "face_image" }) if (body.ContainsKey(f)) profileNew[f] = body[f]!.DeepClone();
        var phone = J.Str(body, "phone", J.Str(current!, "phone")); profileNew["phone_masked"] = phone.Length > 3 ? new string('*', phone.Length - 3) + phone[^3..] : "";
        await store.Add("kycprofiles", profileNew);
        return J.Obj(new { success = true, message = "Gửi hồ sơ thành công; đang chờ quản trị viên duyệt", data = new { kyc_id = J.Int(profileNew, "kyc_id"), verification_id = J.Str(profileNew, "kyc_id"), status = "submitted" } });
    }
}
