namespace JCOP.Models
{
    public class OrderItem
    {
        public string OrderItemId { get; set; }
        public double InvNum { get; set; }
        public string ItemNum { get; set; }
        public double Quantity { get; set; }
        public decimal Price { get; set; }
        public string ImageFilename { get; set; }
    }
}