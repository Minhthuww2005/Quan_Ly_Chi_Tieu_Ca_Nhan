using RevenueManagement.Api.Domain.Enums;
using RevenueManagement.Api.Features.Revenues;
using RevenueManagement.UnitTests.TestSupport;
using Xunit;

namespace RevenueManagement.UnitTests;

public sealed class RevenueValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TestTimeProvider Clock = new(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero));

    // ---------- Quy tắc thuần ----------

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.0001")]
    public void Amount_must_be_positive(string amount)
        => Assert.Contains("Amount phải lớn hơn 0.", RevenueValidator.ValidateAmount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0.0001")]
    [InlineData("1500000")]
    [InlineData("999999999999999.9999")]
    [InlineData("1.50000")]
    public void Valid_amounts_pass(string amount)
        => Assert.Empty(RevenueValidator.ValidateAmount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Amount_with_more_than_four_decimals_is_rejected()
        => Assert.NotEmpty(RevenueValidator.ValidateAmount(1.23456m));

    [Fact]
    public void Amount_over_numeric_19_4_is_rejected()
        => Assert.NotEmpty(RevenueValidator.ValidateAmount(1_000_000_000_000_000m));

    [Fact]
    public void OccurredOn_in_future_is_rejected_but_one_day_slack_is_allowed()
    {
        var today = new DateOnly(2026, 10, 7);
        Assert.Empty(RevenueValidator.ValidateOccurredOn(today.AddDays(1), today));
        Assert.NotEmpty(RevenueValidator.ValidateOccurredOn(today.AddDays(2), today));
        Assert.NotEmpty(RevenueValidator.ValidateOccurredOn(new DateOnly(1999, 12, 31), today));
    }

    [Fact]
    public void Note_longer_than_500_is_rejected()
    {
        Assert.Empty(RevenueValidator.ValidateNote(new string('a', 500)));
        Assert.NotEmpty(RevenueValidator.ValidateNote(new string('a', 501)));
    }

    // ---------- Tham chiếu Wallet/Category (database) ----------

    [Fact]
    public async Task Member_with_income_category_of_same_wallet_is_valid()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var validator = new RevenueValidator(db, Clock);

        var result = await validator.ValidateReferencesAsync(TestDb.OwnerId, TestDb.WalletId, TestDb.CategoryId, Ct);

        Assert.True(result.IsValid);
        Assert.Equal(TestDb.GroupId, result.GroupId);
    }

    [Fact]
    public async Task Wallet_of_other_group_is_reported_as_not_found()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var validator = new RevenueValidator(db, Clock);

        var result = await validator.ValidateReferencesAsync(TestDb.OwnerId, TestDb.OtherGroupWalletId, null, Ct);

        Assert.True(result.WalletNotFound);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Member_who_left_group_cannot_use_wallet()
    {
        var testDb = TestDb.CreateSeeded();
        await using (var setup = testDb.CreateContext())
        {
            var member = setup.GroupMembers.Single(m => m.UserId == TestDb.OwnerId);
            member.LeftAt = DateTime.UtcNow;
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await new RevenueValidator(db, Clock)
            .ValidateReferencesAsync(TestDb.OwnerId, TestDb.WalletId, null, Ct);

        Assert.True(result.WalletNotFound);
    }

    [Fact]
    public async Task Category_from_another_wallet_is_rejected()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();

        var result = await new RevenueValidator(db, Clock)
            .ValidateReferencesAsync(TestDb.OwnerId, TestDb.WalletId, TestDb.OtherWalletCategoryId, Ct);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("CategoryId"));
    }

    [Fact]
    public async Task Non_income_category_is_rejected()
    {
        var testDb = TestDb.CreateSeeded();
        await using (var setup = testDb.CreateContext())
        {
            setup.Categories.Single(c => c.Id == TestDb.CategoryId).Kind = (TransactionKind)2;
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await new RevenueValidator(db, Clock)
            .ValidateReferencesAsync(TestDb.OwnerId, TestDb.WalletId, TestDb.CategoryId, Ct);

        Assert.Contains("Danh mục phải là loại Income.", result.Errors["CategoryId"]);
    }

    [Fact]
    public async Task Archived_wallet_is_rejected()
    {
        var testDb = TestDb.CreateSeeded();
        await using (var setup = testDb.CreateContext())
        {
            setup.Wallets.Single(w => w.Id == TestDb.WalletId).IsArchived = true;
            await setup.SaveChangesAsync(Ct);
        }

        await using var db = testDb.CreateContext();
        var result = await new RevenueValidator(db, Clock)
            .ValidateReferencesAsync(TestDb.OwnerId, TestDb.WalletId, null, Ct);

        Assert.True(result.Errors.ContainsKey("WalletId"));
    }

    [Fact]
    public async Task Full_validation_collects_amount_date_and_note_errors()
    {
        var testDb = TestDb.CreateSeeded();
        await using var db = testDb.CreateContext();
        var input = new RevenueInput(TestDb.WalletId, TestDb.CategoryId, 0m, new DateOnly(2030, 1, 1), new string('x', 501));

        var result = await new RevenueValidator(db, Clock).ValidateAsync(input, TestDb.OwnerId, Ct);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(nameof(RevenueInput.Amount)));
        Assert.True(result.Errors.ContainsKey(nameof(RevenueInput.OccurredOn)));
        Assert.True(result.Errors.ContainsKey(nameof(RevenueInput.Note)));
    }
}
