namespace JCOP.Models
{
    public class ItemCategory
    {
        public string CategoryName { get; set; }
        public string ImageFilename { get; set; }
        public int Page { get; set; }
        public int PerPage { get; set; }
    }

    public class ItemData
    {
        public string ItemNum { get; set; }
        public string ItemName { get; set; }
        public string Barcode { get; set; }
        public string Price { get; set; }
        public string ImageFilename { get; set; }
    }

}
