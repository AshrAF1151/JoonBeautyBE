using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JCOP.Models
{
    public class Customer
    {
        public string CustomerNumber { get; set; }
        public string ShopID { get; set; }
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
        public string Company_name { get; set; }
        public string Street_Address1 { get; set; }
        public string Street_Address2 { get; set; }
        public string City{ get; set; }
        public string State { get; set; }
        public string Zip_Code { get; set; }
        public string Phone_Number { get; set; }
        public string CellPhone_Number{ get; set; }
        public string Discount_Level { get; set; }
        public string Credit { get; set; }
        public string OA_Balance_Due { get; set; }
        public string OA_Max_Balance_Allowed { get; set; }
        public string Bonus_Plan { get; set; }
        public string Bonus_Points_Achieved { get; set; }
        public string Tax_Exempt { get; set; }
        public string Member_Exp { get; set; }
        public string Event_Date1 { get; set; }
        public string Event_Desc1 { get; set; }
        public string Event_Date2 { get; set; }
        public string Event_Desc2 { get; set; }
        public string Event_Date3 { get; set; }
        public string Event_Desc3 { get; set; }
        public string Email { get; set; }
        public string Term { get; set; }
        public string Shipping { get; set; }
        public string Double_Bonus { get; set; }
        public string BirthDay { get; set; }
        public string ShipFirstName { get; set; }
        public string ShipLastName { get; set; }
        public string ShipCompanyName{ get; set; }
        public string ShipStreetAddress{ get; set; }
        public string ShipCity{ get; set; }
        public string ShipState{ get; set; }
        public string ShipZipCode{ get; set; }
        public string ShipPhoneNumber{ get; set; }
        public string LastInvDate{ get; set; }
        public string isMailReturn{ get; set; }
        public string MailReturnDate{ get; set; }
        public string UpdateDate{ get; set; }
        public string HowToPay{ get; set; }
        public string ACHEmail{ get; set; }
        public string PaymentTerm { get; set; }
    }
}
