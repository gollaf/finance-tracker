using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Worker.IntegrationTests
{
    /// <summary>
    /// Stands in for Groq: always "suggests" the offered Category with the
    /// given name, and counts how often it was asked. Everything else in the
    /// pipeline under test is real.
    /// </summary>
    public sealed class StubCategorySuggester : ICategorySuggester
    {
        private readonly string _categoryName;
        private int _calls;

        public StubCategorySuggester(string categoryName)
        {
            _categoryName = categoryName;
        }

        public int Calls => Volatile.Read(ref _calls);

        public Task<Result<CategoryId?>> SuggestAsync(
            CategorySuggestionRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);

            var match = request.Categories.FirstOrDefault(c => c.Name == _categoryName);
            return Task.FromResult(Result.Success<CategoryId?>(match?.Id));
        }
    }
}
