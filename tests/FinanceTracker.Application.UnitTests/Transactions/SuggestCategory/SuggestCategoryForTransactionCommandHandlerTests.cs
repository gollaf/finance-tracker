using FinanceTracker.Application.Categories;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Application.Transactions.SuggestCategory;
using FinanceTracker.Domain.Categories;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinanceTracker.Application.UnitTests.Transactions.SuggestCategory
{
    public class SuggestCategoryForTransactionCommandHandlerTests
    {
        private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

        private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
        private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
        private readonly ICategorySuggester _categorySuggester = Substitute.For<ICategorySuggester>();

        private readonly Category _groceries = Category.Create("Groceries");
        private readonly Category _transport = Category.Create("Transport");

        private SuggestCategoryForTransactionCommandHandler NewHandler() =>
            new(_transactionRepository, _categoryRepository, _categorySuggester);

        private Transaction GivenTransaction(CategoryId? categoryId = null)
        {
            var transaction = Transaction.Create(
                AccountId.New(), Money.Create(18m, "USD"), TransactionType.Expense, "UBER *TRIP", Today, categoryId);

            _transactionRepository.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);
            return transaction;
        }

        private void GivenCategories(params Category[] categories)
        {
            IReadOnlyList<Category> list = categories;
            _categoryRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(list);
        }

        private void GivenSuggestion(Result<CategoryId?> suggestion) =>
            _categorySuggester
                .SuggestAsync(Arg.Any<CategorySuggestionRequest>(), Arg.Any<CancellationToken>())
                .Returns(suggestion);

        private async Task<CategorySuggestionOutcome> HandleAsync(TransactionId transactionId)
        {
            var result = await NewHandler().Handle(
                new SuggestCategoryForTransactionCommand(transactionId), CancellationToken.None);

            // Every expected situation is a success with an outcome; see
            // SuggestCategoryForTransactionCommand's remarks.
            result.IsSuccess.Should().BeTrue();
            return result.Value;
        }

        [Fact]
        public async Task Handle_WhenAiPicksAnOfferedCategory_SavesItWithConditionalUpdate()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Success<CategoryId?>(_transport.Id));
            _transactionRepository
                .TrySetCategoryIfUncategorizedAsync(transaction.Id, _transport.Id, Arg.Any<CancellationToken>())
                .Returns(true);

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.Categorized);
            await _transactionRepository.Received(1)
                .TrySetCategoryIfUncategorizedAsync(transaction.Id, _transport.Id, Arg.Any<CancellationToken>());
            // Never the unconditional update -- it could overwrite a
            // category the user set while the AI was thinking.
            await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_SendsTheDescriptionTypeAndEveryCategoryToTheSuggester()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Success<CategoryId?>(null));

            await HandleAsync(transaction.Id);

            await _categorySuggester.Received(1).SuggestAsync(
                Arg.Is<CategorySuggestionRequest>(r =>
                    r.Description == "UBER *TRIP"
                    && r.Type == TransactionType.Expense
                    && r.Categories.Count == 2
                    && r.Categories[0].Id == _groceries.Id && r.Categories[0].Name == "Groceries"
                    && r.Categories[1].Id == _transport.Id && r.Categories[1].Name == "Transport"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenUserCategorizedItDuringTheAiCall_ReportsAlreadyCategorized()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Success<CategoryId?>(_transport.Id));
            _transactionRepository
                .TrySetCategoryIfUncategorizedAsync(transaction.Id, _transport.Id, Arg.Any<CancellationToken>())
                .Returns(false);

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.AlreadyCategorized);
        }

        [Fact]
        public async Task Handle_WhenTransactionAlreadyHasACategory_DoesNotAskTheAi()
        {
            var transaction = GivenTransaction(_groceries.Id);

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.AlreadyCategorized);
            await _categorySuggester.DidNotReceive()
                .SuggestAsync(Arg.Any<CategorySuggestionRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenTransactionNoLongerExists_ReportsNotFoundWithoutFailing()
        {
            _transactionRepository
                .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
                .Returns((Transaction?)null);

            var outcome = await HandleAsync(TransactionId.New());

            outcome.Should().Be(CategorySuggestionOutcome.TransactionNotFound);
            await _categorySuggester.DidNotReceive()
                .SuggestAsync(Arg.Any<CategorySuggestionRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenNoCategoriesExist_DoesNotAskTheAi()
        {
            var transaction = GivenTransaction();
            GivenCategories();

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.NoCategoriesDefined);
            await _categorySuggester.DidNotReceive()
                .SuggestAsync(Arg.Any<CategorySuggestionRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_WhenAiSaysNothingFits_LeavesTransactionUncategorized()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Success<CategoryId?>(null));

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.NoSuitableCategory);
            await AssertNothingWasWrittenAsync();
        }

        [Fact]
        public async Task Handle_WhenAiIsUnavailable_LeavesTransactionUncategorizedWithoutFailing()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Failure<CategoryId?>(Error.Failure("Groq.Timeout", "Timed out.")));

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.SuggestionUnavailable);
            await AssertNothingWasWrittenAsync();
        }

        [Fact]
        public async Task Handle_WhenSuggesterReturnsAnIdThatWasNotOffered_NeverWritesIt()
        {
            var transaction = GivenTransaction();
            GivenCategories(_groceries, _transport);
            GivenSuggestion(Result.Success<CategoryId?>(CategoryId.New()));

            var outcome = await HandleAsync(transaction.Id);

            outcome.Should().Be(CategorySuggestionOutcome.SuggestionUnavailable);
            await AssertNothingWasWrittenAsync();
        }

        private async Task AssertNothingWasWrittenAsync()
        {
            await _transactionRepository.DidNotReceive().TrySetCategoryIfUncategorizedAsync(
                Arg.Any<TransactionId>(), Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
            await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        }
    }
}
