using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqExpression = System.Linq.Expressions.Expression;

namespace SupermarketPOS.UI.Services
{
    public class PaginationService<T, TDto> where T : class
    {
        private IQueryable<T> _baseQuery;
        private Func<T, TDto> _mapper;
        private Dictionary<string, LinqExpression> _sortExpressions = new Dictionary<string, LinqExpression>();

        public int CurrentPage { get; private set; } = 1;
        public int PageSize { get; private set; } = 50;
        public int TotalItems { get; private set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public List<TDto> Items { get; private set; } = new List<TDto>();

        public event EventHandler<PaginationResult<TDto>> DataLoaded;

        public void Initialize(IQueryable<T> baseQuery, Func<T, TDto> mapper, int pageSize = 50)
        {
            _baseQuery = baseQuery;
            _mapper = mapper;
            PageSize = pageSize;
            CurrentPage = 1;
        }

        public void RegisterSortColumn<TKey>(string columnName, System.Linq.Expressions.Expression<Func<T, TKey>> keySelector) => _sortExpressions[columnName] = keySelector;

        public void ApplySort<TKey>(System.Linq.Expressions.Expression<Func<T, TKey>> keySelector, bool ascending)
        {
            _baseQuery = ascending ? _baseQuery.OrderBy(keySelector) : _baseQuery.OrderByDescending(keySelector);
        }

        public void ResetFilters(IQueryable<T> freshQuery) { _baseQuery = freshQuery; CurrentPage = 1; }
        public void GoToPage(int page) => CurrentPage = Math.Max(1, Math.Min(page, TotalPages));
        public void GoToFirstPage() => CurrentPage = 1;
        public void SetPageSize(int pageSize) { PageSize = Math.Max(1, pageSize); CurrentPage = 1; }

        public void LoadData()
        {
            try
            {
                TotalItems = _baseQuery.Count();
                int skip = (CurrentPage - 1) * PageSize;
                Items = _baseQuery.Skip(skip).Take(PageSize).ToList().Select(e => _mapper(e)).ToList();
                DataLoaded?.Invoke(this, new PaginationResult<TDto> { Items = Items, TotalItems = TotalItems, CurrentPage = CurrentPage, PageSize = PageSize, TotalPages = TotalPages });
            }
            catch
            {
                Items = new List<TDto>(); TotalItems = 0;
                DataLoaded?.Invoke(this, new PaginationResult<TDto> { Items = Items, TotalItems = 0, CurrentPage = 1, PageSize = PageSize, TotalPages = 0 });
            }
        }
    }

    public class PaginationResult<TDto>
    {
        public List<TDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}