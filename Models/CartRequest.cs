using System.Collections.Generic;

namespace JCOP.Models
{
    public class CartRequest
    {
        public string CustNum { get; set; }
        public List<CartItem> CartItems { get; set; }
    }
}