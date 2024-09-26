using System;

namespace JCOP.Models
{
    public class RecentOrder
    {
        public string CustNum { get; set; }
        public string CustName { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal GrandTotal { get; set; }
        public string InvNum { get; set; }
        public DateTime InvDate { get; set; }
        public string SalesmanName { get; set; }
    }
}
