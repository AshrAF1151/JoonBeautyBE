using System;

namespace JCOP.Models
{
    public class InvoiceFilterModel
    {
        public int OdrStatus { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
