using System;
using System.Collections.Generic;

namespace JCOP.Models
{
    public class Invoice
    {
        public string InvNum { get; set; }
        public string ShopID { get; set; }
        public string CustNum { get; set; }
        public string InvDate { get; set; }
        public string GrandTotal { get; set; }
        public string OdrStatus { get; set; }
        public string PaymentMethod { get; set; }
        public CustomerDetail Customer { get; set; }
        public List<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    }

    public class CustomerDetail
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CompanyName { get; set; }
        public string StreetAddress1 { get; set; }
        public string StreetAddress2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
    }

    public class InvoiceItem
    {
        public string Seqno { get; set; }
        public string ItemNum { get; set; }
        public string Quantity { get; set; }
        public string Cost { get; set; }
        public string Price { get; set; }
        public string Tax { get; set; }
        public string NonInventoryName { get; set; }
        public string KitID { get; set; }
        public string SerialNumber { get; set; }
        public string Note1 { get; set; }
        public string Note2 { get; set; }
        public string Note3 { get; set; }
        public string Note4 { get; set; }
        public string LineDisc { get; set; }
        public string UpdateDate { get; set; }
        public string RegPrice { get; set; }
        public string PickQty { get; set; }
        public string OverrideQty { get; set; }
    }
}