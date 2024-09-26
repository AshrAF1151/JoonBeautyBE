namespace JCOP.Models
{
    public class BrandSearchRequest
    {
        public Brand Brand { get; set; }
        public PaginationRequest Pagination { get; set; }
        public string Category { get; set; }
    }
}
