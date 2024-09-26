using System;

namespace JCOP.Models
{
    public class OrderMaster
    {
        public double InvNum { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal GrandTotal { get; set; }
        public int OdrStatus { get; set; }
    }
}