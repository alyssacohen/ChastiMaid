using ChastiMaid.Services.Contracts;

namespace ChastiMaid.Tests.Services.Contracts;

public class ContractFileDialogsTests
{
    [Theory]
    [InlineData(@"C:\x\game", @"C:\x\game.contract.json")]
    [InlineData(@"C:\x\game.json", @"C:\x\game.contract.json")]
    [InlineData(@"C:\x\game.contract.json", @"C:\x\game.contract.json")]
    [InlineData(@"C:\x\game.CONTRACT.JSON", @"C:\x\game.CONTRACT.JSON")]
    public void WithExtension_AlwaysEndsInContractJson(string chosen, string expected)
    {
        Assert.Equal(expected, ContractFileDialogs.WithExtension(chosen));
    }
}
