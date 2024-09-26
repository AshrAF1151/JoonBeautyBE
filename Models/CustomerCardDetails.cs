namespace JCOP.Models
{
    public class CustomerCardDetails
    {
        public string CustNum { get; set; }
        public string CardNumber { get; set; }
        public string CardHolderName { get; set; }
        public string CVV { get; set; }
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
    }
}