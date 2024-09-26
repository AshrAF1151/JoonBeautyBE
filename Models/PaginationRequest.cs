namespace JCOP.Models
{
    public class PaginationRequest
    {
        public int Page { get; set; }
        public int PerPage { get; set; }
        public string Category { get; set; }
        public string ItemNum { get; set; }
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string bestSeller { get; set; }
    }
}