using JCOP.Models;
using System.Collections.Generic;

public class OrderDetails
{
    public OrderMaster OrderMaster { get; set; }
    public List<OrderItem> OrderItems { get; set; }
}
