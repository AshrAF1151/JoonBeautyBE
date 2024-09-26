using System;

namespace JCOP.Models
{
    public class InvoiceFilter
    {
        public int? OdrStatus { get; set; }
        public int Page { get; set; }
        public int PerPage { get; set; }
    }
}
