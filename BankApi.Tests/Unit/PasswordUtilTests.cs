using BankApi.Services;

namespace BankApi.Tests.Unit;

public class PasswordUtilTests
{
    [Fact]
    public void Hash_ThenVerify_AcceptsTheRightPasswordOnly()
    {
        string hash = PasswordUtil.Hash("correct horse");

        Assert.True(PasswordUtil.Verify(hash, "correct horse"));
        Assert.False(PasswordUtil.Verify(hash, "Correct horse"));
        Assert.False(PasswordUtil.Verify(hash, ""));
    }

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently()
    {
        Assert.NotEqual(PasswordUtil.Hash("pass123"), PasswordUtil.Hash("pass123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pass123")] // a legacy plaintext value, not a hash
    public void Verify_RejectsAnythingThatIsNotAHash_WithoutThrowing(string stored)
    {
        Assert.False(PasswordUtil.Verify(stored, "pass123"));
    }

    [Fact]
    public void LooksHashed_RecognizesHashesButNotPlaintext()
    {
        Assert.True(PasswordUtil.LooksHashed(PasswordUtil.Hash("pass123")));

        Assert.False(PasswordUtil.LooksHashed("pass123"));
        Assert.False(PasswordUtil.LooksHashed("hunter2"));
        Assert.False(PasswordUtil.LooksHashed(""));
        Assert.False(PasswordUtil.LooksHashed(null));
    }
}
