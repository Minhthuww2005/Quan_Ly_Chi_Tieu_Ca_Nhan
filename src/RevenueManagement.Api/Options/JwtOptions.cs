using System.ComponentModel.DataAnnotations;

namespace RevenueManagement.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Issuer không được để trống.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Audience không được để trống.")]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:SigningKey không được để trống.")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey phải có độ dài tối thiểu 32 ký tự (256 bits).")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "Jwt:ExpiryMinutes phải từ 1 đến 1440 phút.")]
    public int ExpiryMinutes { get; set; } = 60;

    [Range(1, 90, ErrorMessage = "Jwt:RefreshExpiryDays phải từ 1 đến 90 ngày.")]
    public int RefreshExpiryDays { get; set; } = 7;
}
