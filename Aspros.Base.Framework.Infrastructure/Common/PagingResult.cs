namespace Aspros.Base.Framework.Infrastructure
{
    public class PagingResult<T>(IEnumerable<T> data, int totalCount, int pageSize)
    {
        public IEnumerable<T> Data { get; } = data;

        public int TotalCount { get; } = totalCount;

        public int PageSize { get; } = pageSize;

        public int TotalPage
        {
            get
            {
                if (TotalCount % PageSize > 0)
                {
                    return TotalCount / PageSize + 1;
                }

                return TotalCount / PageSize;
            }
        }
    }
    public class PagingParams(int pageNo, int pageSize = 10)
    {
        public int PageNo { get; set; } = pageNo < 0 ? 1 : pageNo;

        public int PageSize { get; set; } = pageSize;

        public int Skip => (PageNo - 1) * PageSize;
    }
}
