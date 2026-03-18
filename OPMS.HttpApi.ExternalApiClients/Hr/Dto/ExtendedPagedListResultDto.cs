namespace OPMS.HttpApi.ExternalApiClients.Hr.Dto
{
 
        public class ExtendedPagedListResultDto<T> 
    {
        public ICollection<T> Items { get; set; }

        public int PageNumber { get; private set; }
            public int TotalPages { get; private set; }
            public int PageSize { get; private set; }
            public int CurrentPageSize { get; set; }
            public int CurrentStartIndex { get; set; }
            public int CurrentEndIndex { get; set; }
            public long TotalCount { get; set; }
            public bool HasPrevious => PageNumber > 1;
            public bool HasNext => PageNumber < TotalPages;
        }


    }
