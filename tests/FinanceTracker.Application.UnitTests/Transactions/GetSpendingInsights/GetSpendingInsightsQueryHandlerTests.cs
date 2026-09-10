using FinanceTracker.Application.Accounts;
using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Domain.Accounts;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.GetSpendingInsights
{
    public class GetSpendingInsightsQueryHandlerTests
    {
        private static Account NewAccount() => Account.Create("Checking", AccountType.Checking, "USD");

        private static GetSpendingInsightsQueryHandler NewHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ICategoryRepository categoryRepository,
            IInsightsGenerator insightsGenerator) =>
            new(accountRepository, transactionRepository, categoryRepository, insightsGenerator);

        private static IAccountRepository AccountRepositoryReturning(Account account)
        {
            var repository = Substitute.For<IAccountRepository>();
            repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
            return repository;
        }

        private static ITransactionRepository TransactionRepositoryReturning(
            Account account, params Transaction[] transactions)
        {
            var repository = Substitute.For<ITransactionRepository>();
            repository.GetByAccountIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(transactions);
            return repository;
        }

        private static ICategoryRepository CategoryRepositoryReturning(
            params (CategoryId Id, string Name)[] categories)
        {
            var repository = Substitute.For<ICategoryRepository>();

            foreach (var (id, name) in categories)
            {
                repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Category.Create(name));
            }

            return repository;
        }

        [Fact]
        public async Task Handle_WithGeneratorSuccess_ComputesTrendsAndUsesAiNarrative()
        {
            var account = NewAccount();
            var groceries = CategoryId.New();

            // Current month (June 2026): 50 total. Prior 3 months (Mar-May
            // 2026): 30 each, 90 total -> average 30. (50 - 30) / 30 * 100
            // rounded to 1 decimal place is 66.7.
            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Store", new DateOnly(2026, 6, 5), groceries),
                Transaction.Create(account.Id, Money.Create(20m, "USD"), TransactionType.Expense, "Market", new DateOnly(2026, 6, 10), groceries),
                Transaction.Create(account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Store", new DateOnly(2026, 3, 15), groceries),
                Transaction.Create(account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Store", new DateOnly(2026, 4, 15), groceries),
                Transaction.Create(account.Id, Money.Create(30m, "USD"), TransactionType.Expense, "Store", new DateOnly(2026, 5, 15), groceries),
                Transaction.Create(account.Id, Money.Create(500m, "USD"), TransactionType.Income, "Salary", new DateOnly(2026, 6, 1)),
            };

            var accountRepository = AccountRepositoryReturning(account);
            var transactionRepository = TransactionRepositoryReturning(account, transactions);
            var categoryRepository = CategoryRepositoryReturning((groceries, "Groceries"));

            var insightsGenerator = Substitute.For<IInsightsGenerator>();
            insightsGenerator
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success("You spent more on groceries than usual this month."));

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.NarrativeGeneratedByAi.Should().BeTrue();
            result.Value.Narrative.Should().Be("You spent more on groceries than usual this month.");

            result.Value.Trends.Should().ContainSingle();
            var trend = result.Value.Trends[0];
            trend.CategoryId.Should().Be(groceries);
            trend.CategoryName.Should().Be("Groceries");
            trend.CurrentMonthTotal.Should().Be(Money.Create(50m, "USD"));
            trend.PriorAverageTotal.Should().Be(Money.Create(30m, "USD"));
            trend.PercentChange.Should().Be(66.7m);
        }

        [Fact]
        public async Task Handle_WithGeneratorFailure_FallsBackToTemplatedNarrative()
        {
            var account = NewAccount();
            var groceries = CategoryId.New();

            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(50m, "USD"), TransactionType.Expense, "Store", new DateOnly(2026, 6, 5), groceries),
            };

            var accountRepository = AccountRepositoryReturning(account);
            var transactionRepository = TransactionRepositoryReturning(account, transactions);
            var categoryRepository = CategoryRepositoryReturning((groceries, "Groceries"));

            var insightsGenerator = Substitute.For<IInsightsGenerator>();
            insightsGenerator
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<string>(Error.Failure("Groq.Timeout", "The request timed out.")));

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            // A failed AI call never fails the query -- see ADR 0010.
            result.IsSuccess.Should().BeTrue();
            result.Value.NarrativeGeneratedByAi.Should().BeFalse();
            result.Value.Narrative.Should().Contain("Groceries");
            result.Value.Trends.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_WithNoExpenseHistory_ReturnsCannedMessageWithoutCallingGenerator()
        {
            var account = NewAccount();
            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(500m, "USD"), TransactionType.Income, "Salary", new DateOnly(2026, 6, 1)),
            };

            var accountRepository = AccountRepositoryReturning(account);
            var transactionRepository = TransactionRepositoryReturning(account, transactions);
            var categoryRepository = Substitute.For<ICategoryRepository>();
            var insightsGenerator = Substitute.For<IInsightsGenerator>();

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Trends.Should().BeEmpty();
            result.Value.NarrativeGeneratedByAi.Should().BeFalse();
            result.Value.Narrative.Should().Contain("enough history");

            await insightsGenerator
                .DidNotReceive()
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithNoPriorSpendingInCategory_ReturnsNullPercentChange()
        {
            var account = NewAccount();
            var dining = CategoryId.New();

            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(15m, "USD"), TransactionType.Expense, "Cafe", new DateOnly(2026, 6, 12), dining),
            };

            var accountRepository = AccountRepositoryReturning(account);
            var transactionRepository = TransactionRepositoryReturning(account, transactions);
            var categoryRepository = CategoryRepositoryReturning((dining, "Dining Out"));

            var insightsGenerator = Substitute.For<IInsightsGenerator>();
            insightsGenerator
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success("New spending in Dining Out this month."));

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Value.Trends.Should().ContainSingle();
            result.Value.Trends[0].PriorAverageTotal.Should().Be(Money.Create(0m, "USD"));
            result.Value.Trends[0].PercentChange.Should().BeNull();
        }

        [Fact]
        public async Task Handle_WithUncategorizedExpense_ResolvesCategoryNameAsUncategorized()
        {
            var account = NewAccount();
            var transactions = new[]
            {
                Transaction.Create(account.Id, Money.Create(10m, "USD"), TransactionType.Expense, "Misc", new DateOnly(2026, 6, 1)),
            };

            var accountRepository = AccountRepositoryReturning(account);
            var transactionRepository = TransactionRepositoryReturning(account, transactions);
            var categoryRepository = Substitute.For<ICategoryRepository>();

            var insightsGenerator = Substitute.For<IInsightsGenerator>();
            insightsGenerator
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success("Some uncategorized spending this month."));

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(account.Id, 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Value.Trends.Should().ContainSingle();
            result.Value.Trends[0].CategoryId.Should().BeNull();
            result.Value.Trends[0].CategoryName.Should().Be("Uncategorized");

            await categoryRepository
                .DidNotReceive()
                .GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WithUnknownAccount_ReturnsNotFound()
        {
            var accountRepository = Substitute.For<IAccountRepository>();
            accountRepository
                .GetByIdAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
                .Returns((Account?)null);

            var transactionRepository = Substitute.For<ITransactionRepository>();
            var categoryRepository = Substitute.For<ICategoryRepository>();
            var insightsGenerator = Substitute.For<IInsightsGenerator>();

            var handler = NewHandler(accountRepository, transactionRepository, categoryRepository, insightsGenerator);
            var query = new GetSpendingInsightsQuery(AccountId.New(), 2026, 6);

            var result = await handler.Handle(query, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);

            await insightsGenerator
                .DidNotReceive()
                .GenerateAsync(Arg.Any<InsightsGenerationRequest>(), Arg.Any<CancellationToken>());
        }
    }
}
