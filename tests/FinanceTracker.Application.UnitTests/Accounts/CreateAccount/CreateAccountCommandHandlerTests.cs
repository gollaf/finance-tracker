using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Accounts.CreateAccount;
using FinanceTracker.Domain.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Accounts.CreateAccount
{
    public class CreateAccountCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidCommand_SavesAccountAndReturnsItsId()
        {
            Account? savedAccount = null;
            var repository = Substitute.For<IAccountRepository>();
            repository.AddAsync(Arg.Do<Account>(a => savedAccount = a), Arg.Any<CancellationToken>());

            var handler = new CreateAccountCommandHandler(repository);
            var command = new CreateAccountCommand("Checking", AccountType.Checking, "USD");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            savedAccount.Should().NotBeNull();
            savedAccount!.Name.Should().Be("Checking");
            savedAccount.Type.Should().Be(AccountType.Checking);
            savedAccount.Currency.Should().Be("USD");
            result.Value.Should().Be(savedAccount.Id);
        }
    }
}
