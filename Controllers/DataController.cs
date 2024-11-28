using JCOP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.Crmf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using JCOP.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JCOP.Controllers
{
    public class DataController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;

        public DataController(IConfiguration configuration, IEmailSender emailSender, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _emailSender = emailSender;
            _env = env;
        }


        [HttpPost("UploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file, string itemNum)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (string.IsNullOrEmpty(itemNum))
                return BadRequest("Item number is required.");

            var folderPath = Path.Combine(_env.WebRootPath, "assets");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fileName = $"{itemNum}.jpg";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { path = $"/assets/{fileName}" });
        }


        [HttpPost]
        [Route("getAllBrands")]
        public async Task<ActionResult> GetAllBrands([FromBody] PaginationRequest request)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    int offset = (request.Page - 1) * request.PerPage;
                    int next = offset + request.PerPage;

                    string baseQuery = @"
            WITH CTE AS (
            SELECT 
            i.ItemName, MIN(i.ItemNum) AS ItemNum, MIN(i.Barcode) AS Barcode, 
            MIN(i.Price) AS Price, MIN(i.ItemBrand) AS ItemBrand, MIN(i.UpdateDate) AS UpdateDate, MIN(i.ItemCat) AS ItemCat,
            ROW_NUMBER() OVER (ORDER BY ";
                    if (request.bestSeller == "bestSeller")
                    {
                        baseQuery += "SUM(o.Quantity) DESC, ";
                    }
                    else 
                    {
                        baseQuery += "i.ItemName, ";
                    }

                    baseQuery += @"
            i.ItemName) AS RowNum
            FROM Inventory i
            LEFT JOIN OrderItems o ON i.ItemNum = o.ItemNum
            WHERE i.Price > 0
            AND i.UpdateDate >= DATEADD(YEAR, -3, GETDATE()) AND ISNULL(NULLIF(i.ItemName, ''), NULL) IS NOT NULL";
                    if (!string.IsNullOrEmpty(request.Category))
                    {
                        baseQuery += " AND i.ItemCat = @Category";
                    }

                    if (!string.IsNullOrEmpty(request.ItemName))
                    {
                        baseQuery += @"
            AND (
            i.ItemName LIKE '%' + @ItemName + '%'
            OR LOWER(i.ItemNum) LIKE '%' + @ItemName + '%'
            )";
                    }

                    if (!string.IsNullOrEmpty(request.ItemBrand))
                    {
                        baseQuery += " AND i.ItemBrand = @ItemBrand";
                    }

                    baseQuery += @"
            GROUP BY i.ItemName
            )
            SELECT ItemName, ItemNum, Barcode, Price, ItemBrand, UpdateDate, ItemCat
            FROM CTE
            WHERE RowNum BETWEEN @Offset + 1 AND @Next";

                    SqlCommand command = new SqlCommand(baseQuery, conn);
                    command.Parameters.AddWithValue("@Offset", offset);
                    command.Parameters.AddWithValue("@Next", next);

                    if (!string.IsNullOrEmpty(request.Category))
                    {
                        command.Parameters.AddWithValue("@Category", request.Category);
                    }

                    if (!string.IsNullOrEmpty(request.ItemName))
                    {
                        command.Parameters.AddWithValue("@ItemName", request.ItemName);
                    }

                    if (!string.IsNullOrEmpty(request.ItemBrand))
                    {
                        command.Parameters.AddWithValue("@ItemBrand", request.ItemBrand);
                    }

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        List<Brand> brands = new List<Brand>();

                        while (await reader.ReadAsync())
                        {
                            string itemNum = reader["ItemNum"].ToString();
                            string itemName = reader["ItemName"].ToString();
                            string barcode = reader["Barcode"].ToString();
                            string itemBrand = reader["ItemBrand"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            DateTime updateDate = (DateTime)reader["UpdateDate"];
                            string itemCat = reader["ItemCat"].ToString();
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            brands.Add(new Brand
                            {
                                ItemNum = itemNum,
                                ItemName = string.IsNullOrEmpty(itemName) ? "Unknown Brand" : itemName,
                                Barcode = barcode,
                                ItemBrand = itemBrand,
                                ImageFilename = imageFilename,
                                Price = price,
                                UpdateDate = updateDate,
                                ItemCat = itemCat
                            });
                        }

                        return Ok(new { success = true, message = "OK", data = brands });
                    }
                }
            }
            catch (Exception e)
            {
                return StatusCode(500, new { success = false, message = "Error retrieving items", error = e.Message });
            }
        }


        [HttpPost]
        [Route("ItemCategories")]
        public async Task<ActionResult> ItemCategories()
        {
            try
            {
                //string connectionString = @"Data Source=13.52.54.82,1433\EC2AMAZ-GLMLTJA;Initial Catalog=SparkDB;User ID=sa;Password=Pass123;";
                string connectionString = @"Data Source=192.168.44.2\\SQLEXPRESS; Initial Catalog=SparkDB;User Id=sparkuser; Password=Spark123;";
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString = connectionString;
                conn.Open();
                SqlCommand distinctItemsCommand = new SqlCommand("SELECT DISTINCT TOP 15 ItemCat FROM Inventory", conn);

                List<ItemCategory> distinctCategories = new List<ItemCategory>();

                using (SqlDataReader distinctItemsReader = distinctItemsCommand.ExecuteReader())
                {
                    while (distinctItemsReader.Read())
                    {
                        string categoryName = distinctItemsReader["ItemCat"].ToString();
                        string imageFilename = $"/assets/categories/{categoryName}.jpg";

                        if (System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageFilename.TrimStart('/'))))
                        {
                            distinctCategories.Add(new ItemCategory
                            {
                                CategoryName = categoryName,
                                ImageFilename = imageFilename
                            });
                        }
                    }
                }

                conn.Close();
                return Ok(new { success = true, message = "OK", data = distinctCategories });
            }
            catch (Exception e)
            {
                return Ok(new { statusCode = "401", status = "Failed", message = "Unauthorized, invalid username or password" });
            }
        }

        [HttpPost]
        [Route("getItemByItemNum")]
        public async Task<ActionResult> GetItemByItemNum([FromBody] Brand brand)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT ItemName, Price, ItemNum FROM Inventory WHERE ItemNum = @ItemNum";
                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        command.Parameters.AddWithValue("@ItemNum", brand.ItemNum);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            List<Brand> brands = new List<Brand>();

                            while (await reader.ReadAsync())
                            {
                                string itemName = reader["ItemName"].ToString();
                                string price = string.Format("{0:0.00}", reader["Price"]);
                                string itemNum = reader["ItemNum"].ToString();
                                string imageFilename = $"/assets/{itemNum}.jpg";

                                if (price != "0.00" && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageFilename.TrimStart('/'))))
                                {
                                    brands.Add(new Brand
                                    {
                                        ItemName = itemName,
                                        Price = price,
                                        ItemNum = itemNum,
                                        ImageFilename = imageFilename
                                    });
                                }
                            }

                            if (brands.Count == 0)
                            {
                                return Ok(new { success = false, message = "Product not found" });
                            }

                            return Ok(new { success = true, message = "OK", data = brands });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, statusCode = "401", status = "Failed", message = "Unauthorized, invalid username or password", error = e.Message });
            }
        }



        [HttpPost]
        [Route("getItemByBarcode")]
        public async Task<ActionResult> GetItemByBarcode([FromBody] Brand brand)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString = connectionString;
                conn.Open();
                SqlCommand command = new SqlCommand($"SELECT ItemName, Price, Barcode, ItemNum FROM Inventory WHERE Barcode ='{brand.Barcode}'", conn);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    List<Brand> brands = new List<Brand>();

                    while (reader.Read())
                    {
                        string itemName = reader["ItemName"].ToString();
                        string price = string.Format("{0:0.00}", reader["Price"]);
                        string barcode = reader["Barcode"].ToString();
                        string itemNum = reader["ItemNum"].ToString();
                        string imageFilename = $"/assets/{itemNum}.jpg";
                        //string imageFilename = $"/assets/{itemNum}.{Path.GetExtension(itemNum)}";
                        if (price != "0.00" && System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageFilename.TrimStart('/'))))
                        {
                            brands.Add(new Brand { ItemName = itemName, Price = price, Barcode = barcode, ItemNum = itemNum, ImageFilename = imageFilename });
                        }
                    }
                    conn.Close();
                    return Ok(new { success = true, message = "OK", data = brands });
                }
            }
            catch (Exception e)
            {
                return Ok(new { statusCode = "401", status = "Failed", message = "Unauthorized, invalid username or password" });
            }
        }

        [HttpPost]
        [Route("getItemByName")]
        public async Task<ActionResult> GetItemByName([FromBody] BrandSearchRequest searchRequest)
        {
            try
            {
                Brand brand = searchRequest.Brand;
                PaginationRequest request = searchRequest.Pagination;
                string category = searchRequest.Category;

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    int offset = (request.Page - 1) * request.PerPage;
                    int next = offset + request.PerPage;

                    // Prepare the base SQL query
                    string baseQuery = "SELECT ItemNum, ItemName, Price, Barcode, ItemBrand, ItemCat FROM Inventory WHERE Price > 0";

                    // Conditionally add filters for item name, brand, category, and itemNum
                    string filterQuery = "";

                    // Filter by item name
                    if (!string.IsNullOrEmpty(brand.ItemName))
                    {
                        filterQuery += " AND (ItemName LIKE '%' + @ItemName + '%')";
                    }

                    // Filter by item brand
                    if (!string.IsNullOrEmpty(brand.ItemBrand))
                    {
                        filterQuery += " AND (ItemBrand = @ItemBrand)";
                    }

                    // Filter by category
                    if (!string.IsNullOrEmpty(category))
                    {
                        filterQuery += " AND (ItemCat = @ItemCat)";
                    }

                    // Filter by item number
                    if (!string.IsNullOrEmpty(brand.ItemNum))
                    {
                        filterQuery += " AND (ItemNum = @ItemNum)";
                    }

                    // Finalize the SQL query
                    string finalQuery = @"
                WITH CTE AS (
                    SELECT 
                        ItemNum, ItemName, Price, Barcode, ItemBrand, ItemCat,
                        ROW_NUMBER() OVER (ORDER BY ItemNum) AS RowNum
                    FROM Inventory
                    WHERE Price > 0" + filterQuery + @"
                )
                SELECT ItemNum, ItemName, Price, Barcode, ItemBrand, ItemCat
                FROM CTE
                WHERE RowNum BETWEEN @Offset + 1 AND @Next";

                    SqlCommand command = new SqlCommand(finalQuery, conn);
                    command.Parameters.AddWithValue("@Offset", offset);
                    command.Parameters.AddWithValue("@Next", next);

                    // Add parameters
                    if (!string.IsNullOrEmpty(brand.ItemName))
                    {
                        command.Parameters.AddWithValue("@ItemName", brand.ItemName);
                    }

                    if (!string.IsNullOrEmpty(brand.ItemBrand))
                    {
                        command.Parameters.AddWithValue("@ItemBrand", brand.ItemBrand);
                    }

                    if (!string.IsNullOrEmpty(category))
                    {
                        command.Parameters.AddWithValue("@ItemCat", category);
                    }

                    if (!string.IsNullOrEmpty(brand.ItemNum))
                    {
                        command.Parameters.AddWithValue("@ItemNum", brand.ItemNum);
                    }

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        List<Brand> brands = new List<Brand>();

                        while (await reader.ReadAsync())
                        {
                            string itemName = reader["ItemName"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            string barcode = reader["Barcode"].ToString();
                            string itemNum = reader["ItemNum"].ToString();
                            string imageFilename = $"/assets/{itemNum}.jpg";
                            string itemBrand = reader["ItemBrand"].ToString();
                            string itemCat = reader["ItemCat"].ToString();

                            brands.Add(new Brand
                            {
                                ItemName = itemName,
                                Price = price,
                                Barcode = barcode,
                                ItemNum = itemNum,
                                ImageFilename = imageFilename,
                                ItemBrand = itemBrand,
                                ItemCat = itemCat
                            });
                        }

                        if (brands.Count > 0)
                        {
                            return Ok(new { success = true, message = "OK", data = brands });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "No products found" });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return StatusCode(500, new { success = false, message = "Error retrieving items", error = e.Message });
            }
        }



        [HttpPost]
        [Route("orders")]
        public async Task<ActionResult> GetCustomerOrders([FromBody] Login login)
        {
            try
            {
                string custNum = login.Email;

                if (string.IsNullOrEmpty(custNum))
                {
                    return Ok(new { success = false, message = "Unauthorized, invalid token or session" });
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using SqlConnection conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                using SqlCommand command = new SqlCommand($"SELECT InvDate, GrandTotal, OdrStatus FROM OrderMaster WHERE CustNum = @CustNum", conn);
                command.Parameters.AddWithValue("@CustNum", custNum);

                using SqlDataReader reader = await command.ExecuteReaderAsync();

                List<OrderMaster> orders = new List<OrderMaster>();
                while (await reader.ReadAsync())
                {
                    OrderMaster order = new OrderMaster
                    {
                        //CustNum = reader["CustNum"].ToString(),
                        OrderDate = Convert.ToDateTime(reader["InvDate"]),
                        GrandTotal = Convert.ToDecimal(reader["GrandTotal"]),
                        OdrStatus = Convert.ToInt32(reader["OdrStatus"])
                    };
                    orders.Add(order);
                }

                return Ok(new { success = true, message = "OK", data = orders });
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error retrieving customer orders" });
            }
        }

        [HttpPost]
        [Route("addCardDetails")]
        public async Task<ActionResult> AddCardDetails([FromBody] CustomerCardDetails cardDetails)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string checkCustomerQuery = $"SELECT 1 FROM Customer WHERE CustNum = '{cardDetails.CustNum}'";
                    using (SqlCommand checkCustomerCommand = new SqlCommand(checkCustomerQuery, conn))
                    {
                        bool customerExists = await checkCustomerCommand.ExecuteScalarAsync() != null;

                        if (!customerExists)
                        {
                            return Ok(new { success = false, message = "Customer not found." });
                        }
                    }

                    string checkCardDetailsQuery = $"SELECT 1 FROM CustomerCard WHERE CustNum = '{cardDetails.CustNum}'";
                    using (SqlCommand checkCardDetailsCommand = new SqlCommand(checkCardDetailsQuery, conn))
                    {
                        bool cardDetailsExist = await checkCardDetailsCommand.ExecuteScalarAsync() != null;

                        if (cardDetailsExist)
                        {
                            string updateQuery = @"
                        UPDATE CustomerCard 
                        SET CardNumber = @CardNumber, CardHolderName = @CardHolderName, 
                            CVV = @CVV, ExpiryMonth = @ExpiryMonth, ExpiryYear = @ExpiryYear 
                        WHERE CustNum = @CustNum;
                    ";

                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, conn))
                            {
                                updateCommand.Parameters.AddWithValue("@CustNum", cardDetails.CustNum);
                                updateCommand.Parameters.AddWithValue("@CardNumber", cardDetails.CardNumber);
                                updateCommand.Parameters.AddWithValue("@CardHolderName", cardDetails.CardHolderName);
                                updateCommand.Parameters.AddWithValue("@CVV", cardDetails.CVV);
                                updateCommand.Parameters.AddWithValue("@ExpiryMonth", cardDetails.ExpiryMonth);
                                updateCommand.Parameters.AddWithValue("@ExpiryYear", cardDetails.ExpiryYear);

                                await updateCommand.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // If card details do not exist, insert a new record
                            string insertQuery = @"
                        INSERT INTO CustomerCard (CustNum, CardNumber, CardHolderName, CVV, ExpiryMonth, ExpiryYear)
                        VALUES (@CustNum, @CardNumber, @CardHolderName, @CVV, @ExpiryMonth, @ExpiryYear);
                    ";

                            using (SqlCommand insertCommand = new SqlCommand(insertQuery, conn))
                            {
                                insertCommand.Parameters.AddWithValue("@CustNum", cardDetails.CustNum);
                                insertCommand.Parameters.AddWithValue("@CardNumber", cardDetails.CardNumber);
                                insertCommand.Parameters.AddWithValue("@CardHolderName", cardDetails.CardHolderName);
                                insertCommand.Parameters.AddWithValue("@CVV", cardDetails.CVV);
                                insertCommand.Parameters.AddWithValue("@ExpiryMonth", cardDetails.ExpiryMonth);
                                insertCommand.Parameters.AddWithValue("@ExpiryYear", cardDetails.ExpiryYear);

                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    return Ok(new { success = true, message = "Card details added/updated successfully." });
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error adding/updating card details." });
            }
        }

        [HttpPost]
        [Route("CategoryData")]
        public async Task<ActionResult> GetItemsByCategory([FromBody] string categoryName)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"SELECT ItemNum, ItemName, Price, Barcode FROM Inventory WHERE ItemCat = @CategoryName", conn);
                    command.Parameters.AddWithValue("@CategoryName", categoryName);

                    List<Brand> brands = new List<Brand>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemNum = reader["ItemNum"].ToString();
                            string itemName = reader["ItemName"].ToString();
                            string barcode = reader["Barcode"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            if (price != "0.00")
                            {
                                brands.Add(new Brand
                                {
                                    ItemNum = itemNum,
                                    ItemName = string.IsNullOrEmpty(itemName) ? "Unknown Brand" : itemName,
                                    Barcode = barcode,
                                    ImageFilename = imageFilename,
                                    Price = price
                                });
                            }

                        }
                    }

                    return Ok(new { success = true, message = "OK", data = brands });
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error retrieving items by category." });
            }
        }

        [HttpPost]
        [Route("GetItemsByCategory")]
        public async Task<ActionResult> GetItemsByCategory([FromBody] ItemCategory itemCategory)
        {
            try
            {
                int pageSize = itemCategory.PerPage;
                int pageNumber = itemCategory.Page;

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT ItemNum, ItemName, Price, Barcode 
                        FROM (
                            SELECT *, ROW_NUMBER() OVER (ORDER BY ItemNum) AS RowNumber
                            FROM Inventory
                            WHERE ItemCat = @CategoryName
                        ) AS SubQuery
                        WHERE RowNumber BETWEEN @StartIndex AND @EndIndex";

                    SqlCommand command = new SqlCommand(query, conn);
                    command.Parameters.AddWithValue("@CategoryName", itemCategory.CategoryName);
                    command.Parameters.AddWithValue("@StartIndex", (pageNumber - 1) * pageSize + 1);
                    command.Parameters.AddWithValue("@EndIndex", pageNumber * pageSize);

                    List<ItemData> items = new List<ItemData>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemNum = reader["ItemNum"].ToString();
                            string itemName = reader["ItemName"].ToString();
                            string barcode = reader["Barcode"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            if (price != "0.00")
                            {
                                items.Add(new ItemData
                                {
                                    ItemNum = itemNum,
                                    ItemName = string.IsNullOrEmpty(itemName) ? "Unknown Item" : itemName,
                                    Barcode = barcode,
                                    Price = price,
                                    ImageFilename = imageFilename
                                });
                            }
                        }
                    }

                    return Ok(new { success = true, message = "OK", data = items });
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error retrieving items by category." });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("getCardDetails")]
        public async Task<ActionResult> GetCardDetails([FromBody] string custNum)
        {
            try
            {
                var userClaims = User.Identity as ClaimsIdentity;
                string loggedInCustNum = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(loggedInCustNum))
                {
                    return Unauthorized("Invalid token.");
                }

                if (loggedInCustNum != custNum)
                {
                    return Unauthorized("You do not have permission to access this resource.");
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"SELECT CustNum, CardNumber, CardHolderName, CVV, ExpiryMonth, ExpiryYear FROM CustomerCard WHERE CustNum = @CustNum", conn);
                    command.Parameters.AddWithValue("@CustNum", custNum);

                    List<CustomerCardDetails> cards = new List<CustomerCardDetails>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string cardNumber = reader["CardNumber"].ToString();
                            string cardHolderName = reader["CardHolderName"].ToString();
                            string cvv = reader["CVV"].ToString();
                            int expiryMonth = Convert.ToInt32(reader["ExpiryMonth"]);
                            int expiryYear = Convert.ToInt32(reader["ExpiryYear"]);

                            cards.Add(new CustomerCardDetails
                            {
                                CustNum = custNum,
                                CardNumber = cardNumber,
                                CardHolderName = cardHolderName,
                                CVV = cvv,
                                ExpiryMonth = expiryMonth,
                                ExpiryYear = expiryYear
                            });
                        }
                    }

                    return Ok(new { success = true, message = "OK", data = cards });
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error retrieving card details." });
            }
        }

        [HttpPost]
        [Route("createOrder")]
        public async Task<ActionResult> CreateOrder([FromBody] List<CartItem> cartItems)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    foreach (var item in cartItems)
                    {
                        SqlCommand command = new SqlCommand(@"
                    INSERT INTO CustOrders (CustNum, ItemNum, ItemName, Price, Quantity)
                    VALUES (@CustNum, @ItemNum, @ItemName, @Price, @Quantity);
                ", conn);

                        command.Parameters.AddWithValue("@CustNum", item.CustNum);
                        command.Parameters.AddWithValue("@ItemNum", item.ItemNum);
                        //command.Parameters.AddWithValue("@ItemName", item.Name);
                        command.Parameters.AddWithValue("@Price", item.Price);
                        command.Parameters.AddWithValue("@Quantity", item.Quantity);

                        await command.ExecuteNonQueryAsync();
                    }

                    return Ok(new { success = true, message = "Order created successfully." });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error creating order: {e.Message}");
                return Ok(new { success = false, message = "Error creating order." });
            }
        }


        [HttpPost]
        [Route("getOrdersByCustomer")]
        public async Task<ActionResult> GetOrdersByCustomer([FromBody] string custNum)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"SELECT ItemName, Price, Quantity, ItemNum FROM CustOrders WHERE CustNum = @CustNum", conn);
                    command.Parameters.AddWithValue("@CustNum", custNum);

                    List<OrderDetail> orderDetails = new List<OrderDetail>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemName = reader["ItemName"].ToString();
                            string itemNum = reader["ItemNum"].ToString();
                            //string custNum = reader["CustNum"].ToString();
                            string price = reader["Price"].ToString();
                            int quantity = Convert.ToInt32(reader["Quantity"]);
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            orderDetails.Add(new OrderDetail
                            {
                                ItemName = itemName,
                                ItemNum = itemNum,
                                //IustNum = custNum
                                Price = price,
                                Quantity = quantity,
                                ImageFilename = imageFilename
                            });
                        }
                    }

                    return Ok(new { success = true, message = "OK", data = orderDetails });
                }
            }
            catch (Exception e)
            {
                return Ok(new { success = false, message = "Error retrieving orders for the customer." });
            }
        }

        [HttpPost]
        [Route("updateDeliveryAddress")]
        public async Task<ActionResult> UpdateDeliveryAddress([FromBody] DeliveryAddress model)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string updateQuery = @"
            UPDATE Customer 
            SET Street_Address_1 = @StreetAddress, City = @City
            WHERE CustNum = @CustNum;
            ";

                    using (SqlCommand command = new SqlCommand(updateQuery, conn))
                    {
                        command.Parameters.AddWithValue("@CustNum", model.CustNum);
                        command.Parameters.AddWithValue("@StreetAddress", (object)model.StreetAddress ?? DBNull.Value);
                        command.Parameters.AddWithValue("@City", (object)model.City ?? DBNull.Value);
                        //command.Parameters.AddWithValue("@PhoneNumber", (object)model.PhoneNumber ?? DBNull.Value);
                        //command.Parameters.AddWithValue("@FirstName", (object)model.FirstName ?? DBNull.Value);
                        //command.Parameters.AddWithValue("@LastName", (object)model.LastName ?? DBNull.Value);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { success = true, message = "Delivery address updated successfully." });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "Customer not found." });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return StatusCode(500, new { success = false, message = "Internal server error." });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("getCustomerInfo")]
        public async Task<ActionResult> GetCustomerInfo([FromBody] string custNum)
        {
            try
            {
                var userClaims = User.Identity as ClaimsIdentity;
                string loggedInCustNum = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(loggedInCustNum))
                {
                    return Unauthorized("Invalid token.");
                }

                if (loggedInCustNum != custNum)
                {
                    return Unauthorized("You do not have permission to access this resource.");
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                SELECT Street_Address_1, City, Phone_Number, First_name, Last_name 
                FROM Customer 
                WHERE CustNum = @CustNum;
            ";

                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        command.Parameters.AddWithValue("@CustNum", custNum);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.Read())
                            {
                                string streetAddress = reader["Street_Address_1"] != DBNull.Value ? reader.GetString("Street_Address_1") : null;
                                string city = reader["City"] != DBNull.Value ? reader.GetString("City") : null;
                                string phoneNumber = reader["Phone_Number"] != DBNull.Value ? reader.GetString("Phone_Number") : null;
                                string firstName = reader["First_name"] != DBNull.Value ? reader.GetString("First_name") : null;
                                string lastName = reader["Last_name"] != DBNull.Value ? reader.GetString("Last_name") : null;

                                return Ok(new { success = true, message = "Customer information retrieved successfully.", data = new { StreetAddress = streetAddress, City = city, PhoneNumber = phoneNumber, FirstName = firstName, LastName = lastName } });
                            }
                            else
                            {
                                return NotFound(new { success = false, message = "Customer not found." });
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return StatusCode(500, new { success = false, message = "Internal server error." });
            }
        }

        [Authorize]
        [HttpPost]
        [Route("OrderItems")]
        public async Task<ActionResult> AddOrderItems([FromBody] CartRequest request)
        {
            try
            {
                if (request == null || request.CartItems == null || request.CartItems.Count == 0)
                {
                    return BadRequest("Cart items are null or empty.");
                }

                // Extracting customer number from JWT token
                var userClaims = User.Identity as ClaimsIdentity;
                string custNum = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string customerEmail = userClaims.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(custNum))
                {
                    return Unauthorized("Invalid token.");
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        decimal grandTotal = 0;

                        SqlCommand getMaxOrderMasterInvNumCommand = new SqlCommand("SELECT ISNULL(MAX(InvNum), 0) FROM OrderMaster", conn, transaction);
                        object orderMasterResult = await getMaxOrderMasterInvNumCommand.ExecuteScalarAsync();
                        int maxOrderMasterInvNum = Convert.ToInt32(orderMasterResult);

                        SqlCommand getMaxOrderItemsInvNumCommand = new SqlCommand("SELECT ISNULL(MAX(InvNum), 0) FROM OrderItems", conn, transaction);
                        object orderItemsResult = await getMaxOrderItemsInvNumCommand.ExecuteScalarAsync();
                        int maxOrderItemsInvNum = Convert.ToInt32(orderItemsResult);

                        int newInvNum = Math.Max(maxOrderMasterInvNum, maxOrderItemsInvNum) + 1;

                        foreach (var item in request.CartItems)
                        {
                            grandTotal += item.Price * item.Quantity;

                            // Increment Seqno for each item
                            int seqno = await GetNextSeqnoForInvNum(newInvNum, conn, transaction);

                            SqlCommand insertOrderItemCommand = new SqlCommand(@"
INSERT INTO OrderItems (ShopID, InvNum, Seqno, ItemNum, Quantity, Cost, Price, Tax, Non_Inventory_Name, KitID, Serial_Number, Note1, Note2, Note3, Note4, LineDisc, UpdateDate, RegPrice, PickQty)
VALUES (@ShopID, @InvNum, @Seqno, @ItemNum, @Quantity, @Cost, @Price, @Tax, @Non_Inventory_Name, @KitID, @Serial_Number, @Note1, @Note2, @Note3, @Note4, @LineDisc, @UpdateDate, @RegPrice, @PickQty);
", conn, transaction);
                            insertOrderItemCommand.Parameters.AddWithValue("@InvNum", newInvNum);
                            insertOrderItemCommand.Parameters.AddWithValue("@ShopID", "01");
                            insertOrderItemCommand.Parameters.AddWithValue("@Seqno", seqno);
                            insertOrderItemCommand.Parameters.AddWithValue("@ItemNum", item.ItemNum);
                            insertOrderItemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                            insertOrderItemCommand.Parameters.AddWithValue("@Cost", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Price", item.Price);
                            insertOrderItemCommand.Parameters.AddWithValue("@Tax", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Non_Inventory_Name", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@KitID", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Serial_Number", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Note1", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Note2", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Note3", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@Note4", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@LineDisc", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@UpdateDate", DateTime.Now);
                            insertOrderItemCommand.Parameters.AddWithValue("@RegPrice", DBNull.Value);
                            insertOrderItemCommand.Parameters.AddWithValue("@PickQty", DBNull.Value);

                            await insertOrderItemCommand.ExecuteNonQueryAsync();
                        }

                        SqlCommand insertOrderMasterCommand = new SqlCommand(@"
INSERT INTO OrderMaster (
    ShopID, InvNum, OrderDate, GrandTotal, OdrStatus, CustNum, StationID, CashierID, InvDate, InvTime, PriceTotal, 
    CostTotal, TaxTotal, PrevBalance, Credit, Freight, Discount, DiscountPrice, DiscountTax, DiscountGrandTotal, 
    TenderedAmount, Change, InvStatus, PaymentMethod, OrderNum, Stage, OnHoldID
) VALUES (
    @ShopID, @InvNum, @OrderDate, @GrandTotal, @OdrStatus, @CustNum, @StationID, @CashierID, @InvDate, @InvTime, @PriceTotal, 
    @CostTotal, @TaxTotal, @PrevBalance, @Credit, @Freight, @Discount, @DiscountPrice, @DiscountTax, @DiscountGrandTotal, 
    @TenderedAmount, @Change, @InvStatus, @PaymentMethod, @OrderNum, @Stage, @OnHoldID
);
", conn, transaction);

                        insertOrderMasterCommand.Parameters.AddWithValue("@ShopID", "01");
                        insertOrderMasterCommand.Parameters.AddWithValue("@InvNum", newInvNum);
                        insertOrderMasterCommand.Parameters.AddWithValue("@OrderDate", DateTime.Now);
                        insertOrderMasterCommand.Parameters.AddWithValue("@GrandTotal", grandTotal);

                        int randomOdrStatus = new Random().Next(0, 2) == 0 ? 1 : 4;
                        insertOrderMasterCommand.Parameters.AddWithValue("@OdrStatus", randomOdrStatus);

                        insertOrderMasterCommand.Parameters.AddWithValue("@CustNum", custNum);
                        insertOrderMasterCommand.Parameters.AddWithValue("@StationID", "01");
                        insertOrderMasterCommand.Parameters.AddWithValue("@CashierID", "web");
                        insertOrderMasterCommand.Parameters.AddWithValue("@InvDate", DateTime.Now.ToString("yyyy-MM-dd 00:00:00.000"));
                        insertOrderMasterCommand.Parameters.AddWithValue("@InvTime", DateTime.Now.ToString("hh:mm:ss tt"));
                        insertOrderMasterCommand.Parameters.AddWithValue("@PriceTotal", grandTotal);
                        insertOrderMasterCommand.Parameters.AddWithValue("@CostTotal", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@TaxTotal", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@PrevBalance", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@Credit", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@Freight", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@Discount", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@DiscountPrice", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@DiscountTax", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@DiscountGrandTotal", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@TenderedAmount", 0);
                        insertOrderMasterCommand.Parameters.AddWithValue("@Change", 0);

                        insertOrderMasterCommand.Parameters.AddWithValue("@InvStatus", 'O');
                        insertOrderMasterCommand.Parameters.AddWithValue("@PaymentMethod", "CA");
                        insertOrderMasterCommand.Parameters.AddWithValue("@OrderNum", newInvNum);
                        insertOrderMasterCommand.Parameters.AddWithValue("@Stage", "Pick Pending");
                        insertOrderMasterCommand.Parameters.AddWithValue("@OnHoldID", 'O');

                        await insertOrderMasterCommand.ExecuteNonQueryAsync();

                        transaction.Commit();

                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            var emailSubject = $"New Online Order: Your Joon NYX Order #{newInvNum}";
                            var emailBody = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>New Online Order</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            background-color: #f4f4f4;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 40px;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
        }}
        .email-header {{
            text-align: center;
            color: #333;
            margin-bottom: 20px;
        }}
        .email-content {{
            font-size: 16px;
            color: #555;
        }}
        .email-table {{
            width: 100%;
            margin-top: 20px;
            border-collapse: collapse;
            border: 1px solid #ccc;
        }}
        .email-table th,
        .email-table td {{
            padding: 10px;
            text-align: left;
            border-bottom: 1px solid #ccc;
        }}
        .email-footer {{
            margin-top: 20px;
            color: #777;
            font-size: 14px;
        }}
        .contact-info {{
            margin-top: 20px;
            text-align: center;
        }}
        .contact-info p {{
            margin: 5px 0;
        }}
    </style>
</head>
<body>

    <div class=""email-container"">
        <h2 class=""email-header"">New Online Order Confirmation</h2>

        <div class=""email-content"">
            <p>Dear Customer,</p>

            <p>Your order has been placed successfully.</p>

            <table class=""email-table"">
                <tr>
                    <th>Order Number:</th>
                    <td>#{newInvNum}</td>
                </tr>
                <tr>
                    <th>Grand Total:</th>
                    <td>${grandTotal}</td>
                </tr>
            </table>

            <p>Within 24 to 48 hours, expect the following:</p>
            <ul>
                <li>We will process your order.</li>
                <li>Charge your account / card on file.</li>
                <li>Ship your order.</li>
                <li>Send you an invoice.</li>
            </ul>

            <div class=""contact-info"">
                <p>Please contact us at <a href=""tel:+12142174676"">214-217-4676</a> if you have any questions.</p>
            </div>
        </div>

        <p class=""email-footer"">Best Regards,<br>Team Joon Beauty Line</p>
    </div>

</body>
</html>";

                            await _emailSender.SendEmailAsync(customerEmail, emailSubject, emailBody);
                        }

                        return Ok(new { success = true, message = "Order items added successfully.", InvNum = newInvNum });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error occurred while adding order items: {ex.Message}");
                        return Ok(new { success = false, message = "Error adding order items. See server logs for details." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine("SQL Error occurred while adding order items:");
                foreach (SqlError error in sqlEx.Errors)
                {
                    Console.WriteLine($"Error Number: {error.Number}");
                    Console.WriteLine($"Message: {error.Message}");
                    Console.WriteLine($"Procedure: {error.Procedure}");
                    Console.WriteLine($"Line Number: {error.LineNumber}");
                    Console.WriteLine($"Source: {error.Source}");
                }
                return Ok(new { success = false, message = "SQL error occurred while adding order items. See server logs for details.", details = sqlEx.Errors });
            }
        }

        private async Task<int> GetNextSeqnoForInvNum(int invNum, SqlConnection connection, SqlTransaction transaction)
        {
            SqlCommand command = new SqlCommand("SELECT ISNULL(MAX(Seqno), 0) + 1 FROM OrderItems WHERE InvNum = @InvNum", connection, transaction);
            command.Parameters.AddWithValue("@InvNum", invNum);
            object result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        [HttpPost]
        [Route("OrdersByCustNum")]
        public async Task<ActionResult> GetOrdersByCustNum([FromBody] string custNum)
        {
            try
            {
                var userClaims = User.Identity as ClaimsIdentity;
                string loggedInCustNum = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(loggedInCustNum))
                {
                    return Unauthorized("Invalid token.");
                }

                if (loggedInCustNum != custNum)
                {
                    return Unauthorized("You do not have permission to access this resource.");
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand getOrdersCommand = new SqlCommand(@"
                    Select * from
                    (SELECT TOP 10 *
                    FROM OrderMaster om WHERE om.CustNum = @CustNum
                    ORDER BY om.OrderDate DESC)a
                    INNER JOIN OrderItems oi ON a.InvNum = oi.InvNum
                    ORDER BY a.OrderDate DESC
                    ", conn);

                    getOrdersCommand.Parameters.AddWithValue("@CustNum", custNum);

                    SqlDataReader reader = await getOrdersCommand.ExecuteReaderAsync();

                    Dictionary<double, OrderDetails> orderDetailsMap = new Dictionary<double, OrderDetails>();

                    while (await reader.ReadAsync())
                    {
                        OrderItem orderItem = new OrderItem();

                        int invNumOrdinal = reader.GetOrdinal("InvNum");
                        if (!reader.IsDBNull(invNumOrdinal))
                        {
                            orderItem.InvNum = reader.GetDouble(invNumOrdinal);
                        }

                        int itemNumOrdinal = reader.GetOrdinal("ItemNum");
                        if (!reader.IsDBNull(itemNumOrdinal))
                        {
                            orderItem.ItemNum = reader.GetString(itemNumOrdinal);
                        }

                        int quantityOrdinal = reader.GetOrdinal("Quantity");
                        if (!reader.IsDBNull(quantityOrdinal))
                        {
                            orderItem.Quantity = reader.GetDouble(quantityOrdinal);
                        }

                        int priceOrdinal = reader.GetOrdinal("Price");
                        if (!reader.IsDBNull(priceOrdinal))
                        {
                            orderItem.Price = reader.GetDecimal(priceOrdinal);
                        }

                        string imageFilename = $"/assets/{orderItem.ItemNum}.jpg";

                        orderItem.ImageFilename = imageFilename;

                        double invNum = orderItem.InvNum;

                        OrderMaster orderMaster = new OrderMaster();

                        int invOrdinal = reader.GetOrdinal("InvNum");
                        if (!reader.IsDBNull(invOrdinal))
                        {
                            orderMaster.InvNum = reader.GetDouble(invOrdinal);
                        }

                        int orderDateOrdinal = reader.GetOrdinal("OrderDate");
                        if (!reader.IsDBNull(orderDateOrdinal))
                        {
                            orderMaster.OrderDate = reader.GetDateTime(orderDateOrdinal);
                        }

                        int grandTotalOrdinal = reader.GetOrdinal("GrandTotal");
                        if (!reader.IsDBNull(grandTotalOrdinal))
                        {
                            orderMaster.GrandTotal = reader.GetDecimal(grandTotalOrdinal);
                        }

                        int odrStatusOrdinal = reader.GetOrdinal("OdrStatus");
                        if (!reader.IsDBNull(odrStatusOrdinal))
                        {
                            if (reader.GetFieldType(odrStatusOrdinal) == typeof(string))
                            {
                                if (int.TryParse(reader.GetString(odrStatusOrdinal), out int odrStatus))
                                {
                                    orderMaster.OdrStatus = odrStatus;
                                }
                                else
                                {
                                    orderMaster.OdrStatus = 0;
                                    Console.WriteLine("Warning: Unable to parse OdrStatus as an integer.");
                                }
                            }
                            else
                            {
                                orderMaster.OdrStatus = reader.GetInt32(odrStatusOrdinal);
                            }
                        }

                        List<OrderItem> orderItems;
                        if (!orderDetailsMap.ContainsKey(invNum))
                        {
                            orderItems = new List<OrderItem>();

                            OrderDetails orderDetails = new OrderDetails
                            {
                                OrderMaster = orderMaster,
                                OrderItems = orderItems
                            };

                            orderDetailsMap.Add(invNum, orderDetails);
                        }
                        else
                        {
                            orderItems = orderDetailsMap[invNum].OrderItems;
                        }

                        orderItems.Add(orderItem);
                    }

                    return Ok(orderDetailsMap.Values);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching orders by CustNum: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return Ok(new { success = false, message = "Error fetching orders.", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("getItemBrands")]
        public async Task<ActionResult> GetItemBrands()
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"SELECT DISTINCT [ItemBrand] FROM [SparkDB].[dbo].[Inventory] WHERE [UpdateDate] >= DATEADD(YEAR, -3, GETDATE()) AND ISNULL(NULLIF([ItemBrand], ''), NULL) IS NOT NULL ORDER BY [ItemBrand] ASC", conn);

                    List<ItemBrandInfo> itemBrands = new List<ItemBrandInfo>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemBrand = reader["ItemBrand"].ToString();
                            string imageFilename = $"/assets/brands/{itemBrand}.jpg";

                            //if (System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageFilename.TrimStart('/'))))
                            {
                                itemBrands.Add(new ItemBrandInfo
                                {
                                    ItemBrand = itemBrand,
                                    ImageFilename = imageFilename
                                });
                            }
                        }
                    }

                    return Ok(new { success = true, message = "Item brands retrieved successfully.", data = itemBrands });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching item brands: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error fetching item brands.", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("BrandData")]
        public async Task<ActionResult> GetItemsByBrand([FromBody] string itemBrand)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"SELECT ItemNum, ItemName, Price, Barcode FROM Inventory WHERE ItemBrand = @ItemBrand", conn);
                    command.Parameters.AddWithValue("@ItemBrand", itemBrand);

                    List<ItemData> items = new List<ItemData>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemNum = reader["ItemNum"].ToString();
                            string itemName = reader["ItemName"].ToString();
                            string barcode = reader["Barcode"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            if (price != "0.00")
                            {
                                items.Add(new ItemData
                                {
                                    ItemNum = itemNum,
                                    ItemName = string.IsNullOrEmpty(itemName) ? "Unknown Item" : itemName,
                                    Barcode = barcode,
                                    Price = price,
                                    ImageFilename = imageFilename
                                });
                            }
                        }
                    }

                    return Ok(new { success = true, message = "OK", data = items });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching items by brand: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error retrieving items by brand.", error = ex.Message });
            }
        }

        [HttpPost]
        [Route("GetItemsByBrand")]
        public async Task<ActionResult> GetItemsByBrand([FromBody] ItemBrandInfo itemBrandInfo)
        {
            try
            {
                int pageSize = itemBrandInfo.PerPage;
                int pageNumber = itemBrandInfo.Page;

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT ItemNum, ItemName, Price, Barcode 
                        FROM (
                            SELECT *, ROW_NUMBER() OVER (ORDER BY ItemNum) AS RowNumber
                            FROM Inventory
                            WHERE ItemBrand = @ItemBrand
                        ) AS SubQuery
                        WHERE RowNumber BETWEEN @StartIndex AND @EndIndex";

                    SqlCommand command = new SqlCommand(query, conn);
                    command.Parameters.AddWithValue("@ItemBrand", itemBrandInfo.ItemBrand);
                    command.Parameters.AddWithValue("@StartIndex", (pageNumber - 1) * pageSize + 1);
                    command.Parameters.AddWithValue("@EndIndex", pageNumber * pageSize);

                    List<ItemData> items = new List<ItemData>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemNum = reader["ItemNum"].ToString();
                            string itemName = reader["ItemName"].ToString();
                            string barcode = reader["Barcode"].ToString();
                            string price = string.Format("{0:0.00}", reader["Price"]);
                            string imageFilename = $"/assets/{itemNum}.jpg";

                            if (price != "0.00")
                            {
                                items.Add(new ItemData
                                {
                                    ItemNum = itemNum,
                                    ItemName = string.IsNullOrEmpty(itemName) ? "Unknown Item" : itemName,
                                    Barcode = barcode,
                                    Price = price,
                                    ImageFilename = imageFilename
                                });
                            }
                        }
                    }

                    return Ok(new { success = true, message = "OK", data = items });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching items by brand: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error retrieving items by brand.", error = ex.Message });
            }
        }
        [HttpPost]
        [Route("GetDistinctItemBrands")]
        public async Task<ActionResult> GetDistinctItemBrands([FromBody] string categoryName)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    SqlCommand command = new SqlCommand(@"
                SELECT DISTINCT ItemBrand
                FROM Inventory
                WHERE ItemCat = @CategoryName
                ORDER BY [ItemBrand] ASC
            ", conn);

                    command.Parameters.AddWithValue("@CategoryName", categoryName);

                    List<string> itemBrands = new List<string>();

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            string itemBrand = reader["ItemBrand"].ToString();
                            itemBrands.Add(itemBrand);
                        }
                    }

                    return Ok(new { success = true, message = "Distinct item brands retrieved successfully.", data = itemBrands });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching distinct item brands: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error fetching distinct item brands.", error = ex.Message });
            }
        }
    }
}