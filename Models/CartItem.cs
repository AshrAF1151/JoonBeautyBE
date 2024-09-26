using System;

namespace JCOP.Models
{
    public class CartItem
    {
        public int InvNum { get; set; }
        public string CustNum { get; set; }
        public int Seqno { get; set; }
        public string ItemNum { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}