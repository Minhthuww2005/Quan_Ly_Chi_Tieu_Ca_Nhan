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
        // PostgreSQL answers admin reads with aggregates, joins and LIMIT/OFFSET; other repositories
        // (the in-memory test repository) fall back to the same shapes computed with LINQ.
        var reads = store as IWalletAdminReads;
        if (Action == "stats" && Method == "GET")
            return J.Obj(new { success = true, stats = reads != null ? await reads.AdminStats(Today) : await LegacyStats() });
        if (Action is "users" or "user")
        {
            if (parts.Length > 2)
            {
                Require(int.TryParse(parts[2], out var id), "Mã người dùng không hợp lệ"); var user = await User(id);
                if (Method == "GET")
                {
                    var wallet = await store.FindOne("wallets", "user_id", id.ToString());
                    var profiles = await store.Where("kycprofiles", "user_id", id.ToString());
                    return J.Obj(new { success = true, user = J.Public(user), wallet, kyc = profiles.Select(x => J.Public(x)).ToArray() });
                }
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
            var (limit, page) = PageArgs();
            var (rows, total) = reads != null ? await reads.AdminUsers((page - 1) * limit, limit) : await LegacyUsers((page - 1) * limit, limit);
            var list = rows.Select(x =>
            {
                var d = J.Public(x, "_balance", "_transaction_count", "_kyc_status");
                d["balance"] = J.Money(x, "_balance"); d["transaction_count"] = J.Int(x, "_transaction_count");
                var kyc = J.Str(x, "_kyc_status"); d["kyc_status"] = kyc == "" ? "none" : kyc == "submitted" ? "pending" : kyc; return d;
            }).ToArray();
            return Page(list, total, page, limit, "users");
        }
        if (Action == "transactions" && Method == "GET")
        {
            var (limit, page) = PageArgs();
            var (rows, total, stats) = reads != null ? await reads.AdminTransactions((page - 1) * limit, limit, Today) : await LegacyTransactions((page - 1) * limit, limit);
            var list = rows.Select(x =>
            {
                var d = J.Public(x, "_sender_name");
                d["transaction_id"] = J.Str(x, "trans_id"); d["sender_name"] = J.Str(x, "_sender_name") is { Length: > 0 } name ? name : "Không xác định";
                d["recipient_name"] = J.Str(x, "recipient", J.Str(x, "description")); d["created_at"] = J.Str(x, "timestamp");
                d["status"] = J.Str(x, "status") == "success" ? "completed" : J.Str(x, "status"); d["fee"] ??= JsonValue.Create(0); return d;
            }).ToArray();
            var result = Page(list, total, page, limit, "transactions"); result["stats"] = stats; return result;
        }
        if (Action == "kyc")
        {
            if (Method == "GET")
            {
                if (parts.ElementAtOrDefault(2) == "detail")
                {
                    // Images are loaded only for the profile an admin opens, not for the whole list.
                    var profile = await Find("kycprofiles", "kyc_id", parts.ElementAtOrDefault(3) ?? "");
                    var owner = await store.FindOne("users", "user_id", J.Str(profile, "user_id"));
                    var detail = profile.DeepClone().AsObject();
                    detail["_user_name"] = owner == null ? "" : J.Str(owner, "name"); detail["_user_email"] = owner == null ? "" : J.Str(owner, "email");
                    return J.Obj(new { success = true, request = KycView(detail) });
                }
                var pending = parts.ElementAtOrDefault(2) == "pending";
                var rows = reads != null ? await reads.AdminKyc(pending) : await LegacyKyc(pending);
                var list = rows.Select(KycView).ToArray();
                return J.Obj(new { success = true, kyc_requests = list, requests = list });
            }
            var operation = parts.ElementAtOrDefault(2) is "approve" or "reject" ? parts[2] : parts.ElementAtOrDefault(3);
            var key = parts.ElementAtOrDefault(2) is "approve" or "reject" ? parts.ElementAtOrDefault(3) : parts.ElementAtOrDefault(2);
            Require(operation is "approve" or "reject", "Endpoint không tồn tại", 404);
            var target = await Find("kycprofiles", "kyc_id", key ?? ""); Require(J.Str(target, "status") is "submitted" or "pending", "Hồ sơ đã được xử lý", 409);
            var userKyc = await User(J.Int(target, "user_id"));
            if (operation == "approve") { target["status"] = "verified"; target["verification_level"] = 2; target["verified_at"] = Now; userKyc["kycTier"] = "TIER_2"; if (J.Str(target, "photo_review_status") == "pending") target["photo_review_status"] = "approved"; }
            else { Required("reason"); target["status"] = "rejected"; target["rejection_reason"] = J.Str(body, "reason"); userKyc["kycTier"] = "TIER_1"; }
            target["updated_at"] = Now; await Notify(J.Int(target, "user_id"), "KYC", operation == "approve" ? "Hồ sơ đã được duyệt" : "Hồ sơ bị từ chối: " + J.Str(body, "reason"));
            return J.Obj(new { success = true, message = "Đã xử lý hồ sơ KYC", kyc = J.Public(target) });
        }
        throw new ApiError(404, "Endpoint không tồn tại");
    }

    private (int Limit, int Page) PageArgs() =>
        (Math.Clamp(int.TryParse(request.Query["limit"], out var l) ? l : 100, 1, 200), Math.Max(1, int.TryParse(request.Query["page"], out var p) ? p : 1));

    private static JsonObject Page(JsonObject[] list, int total, int page, int limit, string field)
    {
        var result = J.Obj(new { success = true, pagination = new { total, page, limit, pages = (int)Math.Ceiling((double)total / limit) } });
        result[field] = System.Text.Json.JsonSerializer.SerializeToNode(list); return result;
    }

    private static JsonObject KycView(JsonObject x)
    {
        var d = J.Public(x, "_user_name", "_user_email", "_has_images");
        d["verification_id"] = J.Str(x, "kyc_id"); d["user_name"] = J.Str(x, "_user_name"); d["user_email"] = J.Str(x, "_user_email") is { Length: > 0 } email ? email : J.Str(x, "email");
        d["id_number"] = J.Str(x, "national_id_masked", "Đã mã hóa"); d["date_of_birth"] = "Đã mã hóa"; d["status"] = J.Str(x, "status") == "submitted" ? "pending" : J.Str(x, "status");
        d["has_images"] = x.ContainsKey("_has_images") ? J.Bool(x, "_has_images") || J.Str(x, "_has_images") == "True" : x.ContainsKey("front_image") || x.ContainsKey("back_image") || x.ContainsKey("face_image");
        foreach (var f in new[] { "front_image", "back_image", "face_image" }) d[f] ??= JsonValue.Create("");
        return d;
    }

    // ---- LINQ fallbacks with the same shapes, for repositories without IWalletAdminReads ----
    private async Task<JsonObject> LegacyStats()
    {
        var users = await store.All("users"); var wallets = await store.All("wallets"); var txs = await store.All("transactions"); var kyc = await store.All("kycprofiles");
        return J.Obj(new { totalUsers = users.Count, activeUsers = users.Count(x => !Blocked(x)), totalWallets = wallets.Count, totalBalance = wallets.Sum(x => J.Money(x, "balance")), totalTransactions = txs.Count, pendingKYC = kyc.Count(x => J.Str(x, "status") is "submitted" or "pending"), verifiedKYC = kyc.Count(x => J.Str(x, "status") == "verified"), totalCards = (await store.All("cards")).Count, totalBankAccounts = (await store.All("bankaccounts")).Count });
    }
    private async Task<(List<JsonObject>, int)> LegacyUsers(int offset, int limit)
    {
        var users = await store.All("users"); var wallets = await store.All("wallets"); var txs = await store.All("transactions"); var kyc = await store.All("kycprofiles");
        var rows = users.OrderByDescending(x => J.Str(x, "created_at")).Skip(offset).Take(limit).Select(x =>
        {
            var d = x.DeepClone().AsObject(); var wallet = wallets.FirstOrDefault(w => J.Int(w, "user_id") == J.Int(x, "user_id"));
            d["_balance"] = wallet == null ? 0 : J.Money(wallet, "balance"); d["_transaction_count"] = wallet == null ? 0 : txs.Count(t => J.Int(t, "wallet_id") == J.Int(wallet, "wallet_id"));
            d["_kyc_status"] = kyc.LastOrDefault(k => J.Int(k, "user_id") == J.Int(x, "user_id")) is { } k ? J.Str(k, "status") : null; return d;
        }).ToList();
        return (rows, users.Count);
    }
    private async Task<(List<JsonObject>, int, JsonObject)> LegacyTransactions(int offset, int limit)
    {
        var users = await store.All("users"); var wallets = await store.All("wallets"); var txs = await store.All("transactions");
        var rows = txs.OrderByDescending(x => J.Str(x, "timestamp")).Skip(offset).Take(limit).Select(x =>
        {
            var d = x.DeepClone().AsObject(); var wallet = wallets.FirstOrDefault(w => J.Int(w, "wallet_id") == J.Int(x, "wallet_id"));
            var user = users.FirstOrDefault(u => J.Int(u, "user_id") == (wallet == null ? -1 : J.Int(wallet, "user_id"))); d["_sender_name"] = user == null ? null : J.Str(user, "name"); return d;
        }).ToList();
        var done = txs.Where(x => J.Str(x, "status") is "success" or "completed").ToArray();
        var stats = J.Obj(new { total = txs.Count, completed = done.Length, pending = txs.Count(x => J.Str(x, "status") == "pending"), failed = txs.Count(x => J.Str(x, "status") == "failed"), totalVolume = done.Where(x => J.Money(x, "amount") < 0).Sum(x => -J.Money(x, "amount")), totalFees = done.Sum(x => J.Money(x, "fee")), todayTransactions = txs.Count(x => J.Str(x, "timestamp").StartsWith(Today)), todayVolume = done.Where(x => J.Str(x, "timestamp").StartsWith(Today) && J.Money(x, "amount") < 0).Sum(x => -J.Money(x, "amount")) });
        return (rows, txs.Count, stats);
    }
    private async Task<List<JsonObject>> LegacyKyc(bool pending)
    {
        var users = await store.All("users");
        return (await store.All("kycprofiles")).Where(x => !pending || J.Str(x, "status") is "pending" or "submitted").Select(x =>
        {
            var d = x.DeepClone().AsObject(); var hasImages = d.Remove("front_image") | d.Remove("back_image") | d.Remove("face_image");
            var user = users.FirstOrDefault(u => J.Int(u, "user_id") == J.Int(x, "user_id"));
            d["_user_name"] = user == null ? "" : J.Str(user, "name"); d["_user_email"] = user == null ? "" : J.Str(user, "email"); d["_has_images"] = hasImages; return d;
        }).ToList();
    }

    private async Task<JsonObject> Kyc()
    {
        if (Method == "GET")
        {
            var key = Action == "status" ? parts.ElementAtOrDefault(2) : Action; Require(int.TryParse(key, out var id), "Mã người dùng không hợp lệ"); Own(id);
            var profile = (await store.Where("kycprofiles", "user_id", id.ToString())).LastOrDefault() ?? throw new ApiError(404, "Chưa có thông tin KYC");
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
        // Only this user's profiles and the profiles with the same ID hash are read (no KYC images of other users).
        Require(!(await store.Where("kycprofiles", "national_id_hash", hash)).Any(x => J.Int(x, "user_id") != RequestedId), "CMND/CCCD đã được sử dụng", 409);
        Require(!(await store.Where("kycprofiles", "user_id", RequestedId.ToString())).Any(x => J.Str(x, "status") is "pending" or "submitted" or "verified"), "Đã có hồ sơ đang chờ hoặc được duyệt", 409);
        var profileNew = J.Obj(new { kyc_id = await store.Next("kycprofiles", "kyc_id"), user_id = RequestedId, full_name = J.Str(body, "full_name"), email = J.Str(body, "email", J.Str(current!, "email")), national_id_hash = hash, national_id_encrypted = Encrypt(national), national_id_masked = new string('*', national.Length - 4) + national[^4..], status = "submitted", verification_level = 1, has_biometric = false, aml_check = false, sanctions_check = false, pep_check = false, created_at = Now, updated_at = Now, submitted_at = Now });
        foreach (var f in new[] { "date_of_birth", "phone", "address", "document_number" }) profileNew[f + "_encrypted"] = Encrypt(J.Str(body, f));
        foreach (var f in new[] { "gender", "nationality", "city", "district", "document_type", "document_issue_date", "document_issue_place", "front_image", "back_image", "face_image" }) if (body.ContainsKey(f)) profileNew[f] = body[f]!.DeepClone();
        var phone = J.Str(body, "phone", J.Str(current!, "phone")); profileNew["phone_masked"] = phone.Length > 3 ? new string('*', phone.Length - 3) + phone[^3..] : "";
        await store.Add("kycprofiles", profileNew);
        return J.Obj(new { success = true, message = "Gửi hồ sơ thành công; đang chờ quản trị viên duyệt", data = new { kyc_id = J.Int(profileNew, "kyc_id"), verification_id = J.Str(profileNew, "kyc_id"), status = "submitted" } });
    }
}
