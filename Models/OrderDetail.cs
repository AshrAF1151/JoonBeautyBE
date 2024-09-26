namespace JCOP.Models
{
    public class OrderDetail
    {
        public string ItemName { get; set; }
        public string ItemNum { get; set; }
        //public string CustNum { get; set; }
        public string Price { get; set; }
        public int Quantity { get; set; }
        public string ImageFilename { get; set; }
    }
}
