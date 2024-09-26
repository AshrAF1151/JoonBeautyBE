using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JCOP.Models
{
    public class Brand
    {
        public string ItemNum { get; set; }
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string ItemCat { get; set; }
        public string Price { get; set; }
        public string Barcode { get; set; }
        public DateTime UpdateDate { get; set; }
        public string ImageFilename { get; set; }
    }
}
