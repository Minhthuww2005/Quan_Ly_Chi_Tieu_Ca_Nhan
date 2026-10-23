using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace WalletFunding.Api;

public sealed class AuthService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment, IGoogleIdentityVerifier google) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        return await ExecuteWithReceipt(context, async () => Area == "admin" ? await Login(true) : Area == "security" ? await Security() : await Auth());
    }

    private async Task<JsonObject> Auth()
    {
        if (Method == "GET" && Action == "config") return J.Obj(new { success = true, googleClientId = config["Google:ClientId"] ?? "", vnpayEnabled = !string.IsNullOrWhiteSpace(config["Vnpay:TmnCode"]) && !string.IsNullOrWhiteSpace(config["Vnpay:HashSecret"]), developmentReset = environment.IsDevelopment() });
        if (Method == "GET" && Action == "me") return J.Obj(new { success = true, user = LoginUser(current!) });
        if (Method == "PATCH" && Action == "profile")
        {
            var user = await User(UserId);
            if (body.ContainsKey("name")) { Required("name"); Require(J.Str(body, "name").Length <= 100, "Tên quá dài"); user["name"] = J.Str(body, "name").Trim(); }
            if (body.ContainsKey("phone"))
            {
                var phone = J.Str(body, "phone").Trim();
                Require(System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9]{9,15}$"), "Số điện thoại không hợp lệ");
                var owner = await store.FindOne("users", "phone", phone);
                Require(owner == null || J.Int(owner, "user_id") == UserId, "Số điện thoại đã được sử dụng", 409);
                user["phone"] = phone;
            }
            if (body["settings"] is JsonObject settings) user["settings"] = J.Obj(new { notificationsEnabled = J.Bool(settings, "notificationsEnabled"), hideBalance = J.Bool(settings, "hideBalance") });
            user["updated_at"] = Now;
            return J.Obj(new { success = true, message = "Đã lưu hồ sơ", user = LoginUser(user) });
        }
        Require(Method == "POST", "Endpoint không tồn tại", 404);
        if (Action == "login") return await Login(false);
        if (Action == "register")
        {
            Required("name", "email", "phone", "password");
            var email = J.Str(body, "email").Trim().ToLowerInvariant(); var phone = J.Str(body, "phone").Trim();
            Require(new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email), "Email không hợp lệ");
            Require(System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9]{9,15}$"), "Số điện thoại không hợp lệ");
            Require(J.Str(body, "password").Length >= 6 && J.Str(body, "password").Length <= 72, "Mật khẩu phải có 6–72 ký tự");
            Require(await store.FindOne("users", "email", email, ignoreCase: true) == null && await store.FindOne("users", "phone", phone) == null, "Email hoặc số điện thoại đã được sử dụng", 409);
            var user = J.Obj(new { user_id = await store.Next("users", "user_id"), name = J.Str(body, "name"), email, phone, password = BCrypt.Net.BCrypt.HashPassword(J.Str(body, "password")), role = "user", status = "active", accountStatus = "active", authProvider = "local", kycTier = "TIER_1", created_at = Now, updated_at = Now });
            await store.Add("users", user); await CreateWallet(J.Int(user, "user_id")); StatusCode = 201;
            return J.Obj(new { success = true, message = "Đăng ký thành công", userId = J.Int(user, "user_id") });
        }
        if (Action == "google")
        {
            Required("credential"); var identity = await google.VerifyAsync(J.Str(body, "credential"));
            var email = identity.Email;
            var user = await store.FindOne("users", "email", email, ignoreCase: true);
            // A Google sign-in never takes over an account that was registered with a password:
            // only a Google-created account, or one already linked to this Google subject, may sign in this way.
            if (user != null && J.Str(user, "googleId").Length == 0 && J.Str(user, "authProvider", "local") != "google")
                throw new ApiError(409, "Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng email và mật khẩu.", "GOOGLE_LINK_REQUIRED");
            if (user == null)
            {
                user = J.Obj(new { user_id = await store.Next("users", "user_id"), email, name = identity.Name, avatar = identity.Picture, authProvider = "google", googleId = identity.Subject, role = "user", status = "active", accountStatus = "active", kycTier = "TIER_1", created_at = Now });
                await store.Add("users", user); await CreateWallet(J.Int(user, "user_id"));
            }
            Require(!Blocked(user), "Tài khoản đã bị khóa", 403);
            Require(J.Str(user, "googleId", identity.Subject) == identity.Subject, "Google account không khớp", 401);
            user["googleId"] = identity.Subject;
            await store.Add("securitylogs", J.Obj(new { user_id = J.Int(user, "user_id"), action = "google_login", timestamp = Now }));
            return J.Obj(new { success = true, message = "Đăng nhập thành công", token = Token(user), user = LoginUser(user) });
        }
        if (Action == "forgot-password")
        {
            Required("email"); var result = Ok("Nếu email tồn tại, yêu cầu khôi phục đã được tạo");
            var user = await store.FindOne("users", "email", J.Str(body, "email"), ignoreCase: true);
            if (user != null)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                await store.Add("tokens", J.Obj(new { tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), user_id = J.Int(user, "user_id"), expires = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"), consumed = false }));
                if (environment.IsDevelopment()) result["resetToken"] = token;
                else
                {
                    Require(!string.IsNullOrWhiteSpace(config["Smtp:Host"]), "Chưa cấu hình dịch vụ email", 503);
                    using var smtp = new System.Net.Mail.SmtpClient(config["Smtp:Host"], config.GetValue("Smtp:Port", 587)) { EnableSsl = true, Credentials = new System.Net.NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]) };
                    await smtp.SendMailAsync(config["Smtp:From"]!, J.Str(user, "email"), "HTD PAY: khôi phục mật khẩu", "Mã khôi phục (hết hạn sau 30 phút): " + token);
                }
            }
            return result;
        }
        if (Action == "reset-password")
        {
            Required("token", "newPassword"); var password = J.Str(body, "newPassword"); Require(password.Length is >= 6 and <= 72, "Mật khẩu phải có 6–72 ký tự");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(J.Str(body, "token"))));
            var reset = (await store.All("tokens")).FirstOrDefault(x => J.Str(x, "tokenHash") == hash && !J.Bool(x, "consumed") && DateTimeOffset.TryParse(J.Str(x, "expires"), out var expiry) && expiry > DateTimeOffset.UtcNow);
            Require(reset != null, "Mã khôi phục đã hết hạn hoặc không hợp lệ");
            var user = await User(J.Int(reset!, "user_id")); user["password"] = BCrypt.Net.BCrypt.HashPassword(password); user["updated_at"] = Now; reset!["consumed"] = true;
            return Ok("Đặt lại mật khẩu thành công");
        }
        if (Action == "change-password" || Action == "set-pin") return await Security();
        throw new ApiError(404, "Endpoint không tồn tại");
    }

    private async Task CreateWallet(int userId) => await store.Add("wallets", J.Obj(new { wallet_id = await store.Next("wallets", "wallet_id"), user_id = userId, balance = 0, currency = "VND", status = "active", created_at = Now }));
    private static JsonObject LoginUser(JsonObject user)
    {
        var d = J.Public(user); d["hasPin"] = !string.IsNullOrEmpty(J.Str(user, "pinHash")); return d;
    }
    private async Task<JsonObject> Login(bool admin)
    {
        var identifier = J.Str(body, "identifier", J.Str(body, "email")).Trim(); var password = J.Str(body, "password");
        Require(identifier.Length > 0 && password.Length > 0, "Vui lòng nhập đầy đủ thông tin");
        var key = (admin ? "admin-login:" : "login:") + identifier.ToLowerInvariant();
        var attempts = await store.GetAttempts(key);
        if (attempts.Locked) throw Locked(attempts, "Đăng nhập sai quá nhiều lần", "LOGIN_LOCKED");
        var user = await store.FindOne("users", "email", identifier, ignoreCase: true) ?? (admin ? null : await store.FindOne("users", "phone", identifier));
        if (admin && user != null && J.Str(user, "role") != "admin") user = null;
        if (user == null || !Verify(password, J.Str(user, "password")))
        {
            // Counted outside the request transaction; 5 failures lock this identifier for 15 minutes.
            var failed = await store.RecordFailure(key, MaxFailedAttempts, LockDuration);
            if (failed.Locked) throw Locked(failed, "Đăng nhập sai quá nhiều lần", "LOGIN_LOCKED");
            throw new ApiError(401, "Email/SĐT hoặc mật khẩu không đúng", "VALIDATION_ERROR");
        }
        if (attempts.Failures > 0) await store.ClearAttempts(key);
        Require(!Blocked(user!), "Tài khoản đã bị khóa", 403);
        var result = J.Obj(new { success = true, message = "Đăng nhập thành công", token = Token(user!) });
        var publicUser = LoginUser(user!); if (admin) publicUser["admin_id"] = J.Int(user!, "user_id"); result[admin ? "admin" : "user"] = publicUser;
        await store.Add("securitylogs", J.Obj(new { user_id = J.Int(user!, "user_id"), action = "login", timestamp = Now })); return result;
    }

    private async Task<JsonObject> Security()
    {
        if (Method == "GET")
        {
            var id = int.TryParse(Action, out var n) ? n : UserId; Own(id); var u = await User(id);
            return J.Obj(new { success = true, security = new { has2FA = false, hasPin = !string.IsNullOrEmpty(J.Str(u, "pinHash")), hasBiometric = false, lastPasswordChange = J.Str(u, "updated_at", J.Str(u, "created_at")), loginHistory = (await store.All("securitylogs")).Where(x => J.Int(x, "user_id") == id).TakeLast(20).Reverse().Select(x => J.Public(x)).ToArray() } });
        }
        Require(Method == "POST", "Endpoint không tồn tại", 404); var user = await User(RequestedId);
        if (Action == "verify-password") return J.Obj(new { success = true, valid = Verify(J.Str(body, "password"), J.Str(user, "password")) });
        if (Action == "change-password")
        {
            var old = J.Str(body, "old_password", J.Str(body, "oldPassword")); var password = J.Str(body, "new_password", J.Str(body, "newPassword"));
            Require(Verify(old, J.Str(user, "password")), "Mật khẩu hiện tại không đúng", 401);
            Require(password.Length is >= 6 and <= 72, "Mật khẩu phải có 6–72 ký tự"); user["password"] = BCrypt.Net.BCrypt.HashPassword(password); user["updated_at"] = Now; return Ok("Đổi mật khẩu thành công");
        }
        Require(new[] { "set-pin", "setup-pin", "change-pin", "reset-pin" }.Contains(Action), "Endpoint không tồn tại", 404);
        var pin = J.Str(body, "new_pin", J.Str(body, "pinCode", J.Str(body, "pin")));
        Require(System.Text.RegularExpressions.Regex.IsMatch(pin, "^[0-9]{6}$"), "PIN phải gồm 6 chữ số");
        if (Action == "reset-pin") Require(Verify(J.Str(body, "password"), J.Str(user, "password")), "Mật khẩu không đúng", 401);
        else if (!string.IsNullOrEmpty(J.Str(user, "pinHash"))) await Pin(user, J.Str(body, "old_pin", J.Str(body, "oldPinCode")));
        user["pinHash"] = BCrypt.Net.BCrypt.HashPassword(pin); user["updated_at"] = Now; return Ok("Cập nhật PIN thành công");
    }
}
