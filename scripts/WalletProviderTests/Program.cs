using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WalletFunding.Api;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; }
void Reject(Action action, string name) { try { action(); } catch (ApiError e) when (e.Status == 401) { passed++; return; } throw new Exception(name); }
using var rsa = RSA.Create(2048);
var signingKey = new RsaSecurityKey(rsa) { KeyId = "qa-google-key" };
var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
string GoogleToken(string audience = "qa-client", string issuer = "https://accounts.google.com", bool verified = true, bool expired = false, SigningCredentials? signer = null, bool subject = true)
{
    var claims = new List<Claim> { new("email", "qa-google@example.test"), new("email_verified", verified ? "true" : "false"), new("name", "QA Google") };
    if (subject) claims.Add(new Claim("sub", "qa-google-subject"));
    var now = DateTime.UtcNow;
    return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims, now.AddHours(-2), expired ? now.AddHours(-1) : now.AddMinutes(10), signer ?? credentials));
}
var identity = GoogleIdentityVerifier.Validate(GoogleToken(), "qa-client", [signingKey]);
Check(identity.Subject == "qa-google-subject" && identity.Email == "qa-google@example.test", "Valid Google token must retain raw sub/email claims");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(audience: "another-client"), "qa-client", [signingKey]), "Wrong audience");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(issuer: "https://fake.example"), "qa-client", [signingKey]), "Wrong issuer");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(verified: false), "qa-client", [signingKey]), "Unverified email");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(expired: true), "qa-client", [signingKey]), "Expired Google token");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(subject: false), "qa-client", [signingKey]), "Missing subject");
using var fakeRsa = RSA.Create(2048);
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(signer: new SigningCredentials(new RsaSecurityKey(fakeRsa), SecurityAlgorithms.RsaSha256)), "qa-client", [signingKey]), "Forged signature");
Reject(() => GoogleIdentityVerifier.Validate(GoogleToken(signer: new SigningCredentials(new SymmetricSecurityKey(new byte[64]), SecurityAlgorithms.HmacSha256)), "qa-client", [signingKey]), "Algorithm substitution");

const string secret = "QA_ONLY_VNPAY_SECRET_NOT_A_MERCHANT_KEY";
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Vnpay:TmnCode"] = "TESTHTD1", ["Vnpay:HashSecret"] = secret }).Build();
var gateway = new VnpayGateway(config);
var fields = new Dictionary<string, string> { ["vnp_TmnCode"] = "TESTHTD1", ["vnp_TxnRef"] = "QA123", ["vnp_Amount"] = "10000000", ["vnp_OrderInfo"] = "Thanh toan QA 123", ["vnp_ResponseCode"] = "00" };
// Expected value is independently calculated with a fixed, manually ordered input.
var canonical = "vnp_Amount=10000000&vnp_OrderInfo=Thanh+toan+QA+123&vnp_ResponseCode=00&vnp_TmnCode=TESTHTD1&vnp_TxnRef=QA123";
Check(VnpayGateway.Canonical(fields) == canonical, "VNPay sorting and form-url encoding");
fields["vnp_SecureHash"] = Convert.ToHexString(HMACSHA512.HashData(System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
Check(gateway.Verify(fields), "Valid independent VNPay signature");
fields["vnp_Amount"] = "99999900";
Check(!gateway.Verify(fields), "Tampered amount rejected");
fields["vnp_SecureHash"] = "not-hex";
Check(!gateway.Verify(fields), "Malformed signature rejected");
Check(gateway.CreateUrl("QA123", 100000, "127.0.0.1").Contains("vnp_Amount=10000000"), "Amount multiplied by 100");
Check(gateway.CreateUrl("QA123", 100000, "127.0.0.1").Contains("vnp_ExpireDate="), "Checkout expiration supplied");
var hostile = new VnpayGateway(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Vnpay:TmnCode"] = "TESTHTD1", ["Vnpay:HashSecret"] = secret, ["Vnpay:PaymentUrl"] = "https://evil.example/pay" }).Build());
try { hostile.CreateUrl("QA123", 100000, "127.0.0.1"); throw new Exception("Unexpected gateway allowed"); } catch (ApiError e) when (e.Status == 503) { passed++; }
Console.WriteLine($"PASS: {passed} provider checks; Google claims/signatures and VNPay canonicalization/tampering verified.");
