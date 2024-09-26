using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using JCOP.Models;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Org.BouncyCastle.Asn1.Ocsp;

namespace JCOP.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [Route("topcustomers")]
        public async Task<IActionResult> GetTopCustomers()
        {
            List<TopCustomer> topCustomers = new List<TopCustomer>();
            int activeCustomerCount = 0;
            int totalCustomerCount = 0;
            int invoicesOver45DaysCount = 0;
            int invoicesLastYearNoInvoicesThisYear = 0;
            int noInvoiceCustomersCount = 0;
            int customersLast30DaysCount = 0;

            // This will hold YTD data in the form of { year: { month: amount } }
            Dictionary<int, Dictionary<int, decimal>> ytdInvoiceAmounts = new Dictionary<int, Dictionary<int, decimal>>();

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Queries
            string topCustomersQuery = @"
SELECT TOP 10 
    C.[CustNum], 
    C.[First_name] + ' ' + C.[Last_name] AS CustomerName,
    C.[Phone_Number], 
    C.[Street_Address_1] + ', ' + C.[City] + ', ' + C.[State] + ' ' + C.[Zip_Code] AS Address,
    SUM(OM.[GrandTotal]) AS TotalAmountPurchased
FROM 
    [SparkDB].[dbo].[OrderMaster] OM
INNER JOIN 
    [SparkDB].[dbo].[Customer] C ON OM.[CustNum] = C.[CustNum] AND OM.[ShopID] = C.[ShopID]
WHERE
    OM.[InvDate] >= DATEADD(year, -1, GETDATE())
GROUP BY 
    C.[CustNum], 
    C.[First_name], 
    C.[Last_name], 
    C.[Phone_Number], 
    C.[Street_Address_1], 
    C.[City], 
    C.[State], 
    C.[Zip_Code]
ORDER BY 
    TotalAmountPurchased DESC;";

            string activeCustomersCountQuery = @"
SELECT COUNT(DISTINCT [CustNum]) AS TotalActiveCustomers
FROM [SparkDB].[dbo].[OrderMaster]
WHERE [GrandTotal] > 50
  AND [InvDate] >= DATEADD(MONTH, -13, GETDATE())
";

            string totalCustomersQuery = @"
SELECT COUNT(DISTINCT C.[CustNum])
FROM [SparkDB].[dbo].[Customer] C;";

            string invoicesOver45DaysQuery = @"
SELECT COUNT(DISTINCT OM.[CustNum])
FROM [SparkDB].[dbo].[OrderMaster] OM
WHERE OM.[InvDate] < DATEADD(day, -45, GETDATE());";

            string invoicesLastYearNoInvoicesThisYearQuery = @"
SELECT COUNT(DISTINCT OM.[CustNum])
FROM [SparkDB].[dbo].[OrderMaster] OM
WHERE OM.[InvDate] BETWEEN DATEADD(year, -1, GETDATE()) AND GETDATE()
AND NOT EXISTS (
    SELECT 1 
    FROM [SparkDB].[dbo].[OrderMaster] OM2
    WHERE OM2.[CustNum] = OM.[CustNum]
    AND OM2.[InvDate] >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)
);";

            string noInvoiceCustomersQuery = @"
SELECT COUNT(C.[CustNum])
FROM [SparkDB].[dbo].[Customer] C
LEFT JOIN [SparkDB].[dbo].[OrderMaster] OM ON OM.[CustNum] = C.[CustNum]
WHERE OM.[CustNum] IS NULL;";

            string ytdInvoiceAmountQuery = @"
SELECT
    YEAR(OM.[InvDate]) AS [Year],
    MONTH(OM.[InvDate]) AS [Month],
    SUM(OM.[GrandTotal]) AS [TotalAmount]
FROM [SparkDB].[dbo].[OrderMaster] OM
WHERE OM.[InvDate] >= DATEADD(year, -1, DATEFROMPARTS(YEAR(GETDATE()), 1, 1))
GROUP BY YEAR(OM.[InvDate]), MONTH(OM.[InvDate])
ORDER BY YEAR(OM.[InvDate]), MONTH(OM.[InvDate]);";

            string customersLast30DaysQuery = @"
        SELECT COUNT(DISTINCT OM.[CustNum]) AS TotalCustomersLast30Days
        FROM [SparkDB].[dbo].[OrderMaster] OM
        WHERE OM.[InvDate] >= DATEADD(day, -30, GETDATE());";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Fetch top customers
                    using (SqlCommand command = new SqlCommand(topCustomersQuery, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                topCustomers.Add(new TopCustomer
                                {
                                    CustNum = reader["CustNum"].ToString(),
                                    CustomerName = reader["CustomerName"].ToString(),
                                    PhoneNumber = reader["Phone_Number"].ToString(),
                                    Address = reader["Address"].ToString(),
                                    TotalAmountPurchased = reader.GetDecimal(reader.GetOrdinal("TotalAmountPurchased"))
                                });
                            }
                        }
                    }

                    // Fetch active customers count
                    using (SqlCommand command = new SqlCommand(activeCustomersCountQuery, conn))
                    {
                        activeCustomerCount = (int)await command.ExecuteScalarAsync();
                    }

                    // Fetch total customers count
                    using (SqlCommand command = new SqlCommand(totalCustomersQuery, conn))
                    {
                        totalCustomerCount = (int)await command.ExecuteScalarAsync();
                    }

                    // Fetch invoices over 45 days count
                    using (SqlCommand command = new SqlCommand(invoicesOver45DaysQuery, conn))
                    {
                        invoicesOver45DaysCount = (int)await command.ExecuteScalarAsync();
                    }

                    // Fetch customers with invoices last year but none this year
                    using (SqlCommand command = new SqlCommand(invoicesLastYearNoInvoicesThisYearQuery, conn))
                    {
                        invoicesLastYearNoInvoicesThisYear = (int)await command.ExecuteScalarAsync();
                    }

                    // Fetch customers with no invoices
                    using (SqlCommand command = new SqlCommand(noInvoiceCustomersQuery, conn))
                    {
                        noInvoiceCustomersCount = (int)await command.ExecuteScalarAsync();
                    }

                    // Fetch YTD invoice amounts
                    using (SqlCommand command = new SqlCommand(ytdInvoiceAmountQuery, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int year = reader.GetInt32(reader.GetOrdinal("Year"));
                                int month = reader.GetInt32(reader.GetOrdinal("Month"));
                                decimal totalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"));

                                if (!ytdInvoiceAmounts.ContainsKey(year))
                                {
                                    ytdInvoiceAmounts[year] = new Dictionary<int, decimal>();
                                }

                                ytdInvoiceAmounts[year][month] = totalAmount;
                            }
                        }
                    }

                    // Fetch customers who visited in the last 30 days
                    using (SqlCommand command = new SqlCommand(customersLast30DaysQuery, conn))
                    {
                        customersLast30DaysCount = (int)await command.ExecuteScalarAsync();
                    }
                }

                return Ok(new
                {
                    success = true,
                    customers = topCustomers,
                    totalActiveCustomers = activeCustomerCount,
                    totalCustomers = totalCustomerCount,
                    invoicesOver45DaysCount = invoicesOver45DaysCount,
                    invoicesLastYearNoInvoicesThisYear = invoicesLastYearNoInvoicesThisYear,
                    noInvoiceCustomersCount = noInvoiceCustomersCount,
                    customersLast30DaysCount = customersLast30DaysCount,
                    ytdInvoiceAmounts = ytdInvoiceAmounts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        [Route("topproducts")]
        public async Task<IActionResult> GetTopProducts()
        {
            List<TopProduct> topProducts = new List<TopProduct>();
            int totalActiveItems = 0;
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
WITH ProductSales AS (
    SELECT
        [ItemNum],
        SUM([Quantity]) AS TotalQuantitySold
    FROM [SparkDB].[dbo].[OrderItems]
    WHERE [UpdateDate] >= DATEADD(YEAR, -1, GETDATE())
    GROUP BY [ItemNum]
),
TotalActiveItems AS (
    SELECT COUNT(DISTINCT [ItemNum]) AS TotalActiveItems
    FROM [SparkDB].[dbo].[OrderItems]
    WHERE [UpdateDate] >= DATEADD(YEAR, -1, GETDATE())
)
SELECT TOP 10
    i.[ItemNum],
    i.[ItemName],
    i.[Price],
    i.[ItemBrand],
    ps.TotalQuantitySold,
    tai.TotalActiveItems
FROM ProductSales ps
INNER JOIN [SparkDB].[dbo].[Inventory] i
    ON ps.[ItemNum] = i.[ItemNum]
CROSS JOIN TotalActiveItems tai
ORDER BY ps.TotalQuantitySold DESC
OPTION (MAXRECURSION 0);";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                totalActiveItems = reader.GetInt32(reader.GetOrdinal("TotalActiveItems"));

                                do
                                {
                                    topProducts.Add(new TopProduct
                                    {
                                        ItemNum = reader["ItemNum"].ToString(),
                                        ItemName = reader["ItemName"].ToString(),
                                        Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                                        ItemBrand = reader["ItemBrand"].ToString(),
                                        TotalQuantitySold = reader.GetDouble(reader.GetOrdinal("TotalQuantitySold"))
                                    });
                                } while (await reader.ReadAsync());
                            }
                        }
                    }
                }

                return Ok(new { success = true, products = topProducts, totalActiveItems });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        [Route("topcategories")]
        public async Task<IActionResult> GetTopCategories()
        {
            List<TopCategory> topCategories = new List<TopCategory>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
                SELECT TOP (10)
                inv.[ItemCat] AS [Category],
                SUM(oi.[Quantity]) AS [TotalQuantitySold]
                FROM [SparkDB].[dbo].[OrderItems] oi
                INNER JOIN [SparkDB].[dbo].[Inventory] inv
                ON oi.[ItemNum] = inv.[ItemNum]
                WHERE oi.[UpdateDate] >= DATEADD(YEAR, -1, GETDATE())
                GROUP BY inv.[ItemCat]
                ORDER BY [TotalQuantitySold] DESC;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                topCategories.Add(new TopCategory
                                {
                                    Category = reader["Category"].ToString(),
                                    TotalQuantitySold = reader.GetDouble(reader.GetOrdinal("TotalQuantitySold"))
                                });
                            }
                        }
                    }
                }

                return Ok(new { success = true, categories = topCategories });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("lastmonthsales")]
        public async Task<IActionResult> GetLastMonthSales()
        {
            List<LastMonthSale> lastMonthSales = new List<LastMonthSale>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
            SELECT
            inv.[ItemCat] AS [Category],
            SUM(oi.[Quantity]) AS [TotalQuantitySold],
            SUM(oi.[Price] * oi.[Quantity]) AS [TotalSalesAmount]
            FROM [SparkDB].[dbo].[OrderItems] oi
            INNER JOIN [SparkDB].[dbo].[Inventory] inv
            ON oi.[ItemNum] = inv.[ItemNum]
            WHERE oi.[UpdateDate] >= DATEADD(MONTH, -1, GETDATE())
            AND oi.[UpdateDate] < GETDATE()
            GROUP BY inv.[ItemCat];";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                lastMonthSales.Add(new LastMonthSale
                                {
                                    Category = reader["Category"].ToString(),
                                    TotalQuantitySold = reader.GetDouble(reader.GetOrdinal("TotalQuantitySold")),
                                    TotalSalesAmount = reader.GetDouble(reader.GetOrdinal("TotalSalesAmount"))
                                });
                            }
                        }
                    }
                }

                return Ok(new { success = true, sales = lastMonthSales });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("pendingorderslastmonth")]
        public async Task<IActionResult> GetPendingOrdersLastMonth()
        {
            int pendingOrderCount = 0;
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
            SELECT
            SUM(oi.[Quantity]) AS [TotalQuantitySold],
            SUM(oi.[Price] * oi.[Quantity]) AS [TotalSalesAmount]
            FROM [SparkDB].[dbo].[OrderItems] oi
            WHERE oi.[UpdateDate] >= DATEADD(DAY, -30, GETDATE())
            AND oi.[UpdateDate] < GETDATE();";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, conn))
                    {
                        object result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            pendingOrderCount = Convert.ToInt32(result);
                        }
                    }
                }

                return Ok(new { success = true, pendingOrderCount });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("invoices")]
        public async Task<IActionResult> GetAllInvoices([FromBody] InvoiceFilter filter)
        {
            List<Invoice> invoices = new List<Invoice>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Base query with joins
            string invoiceQuery = @"
        SELECT 
            om.[InvNum], om.[ShopID], om.[CustNum], om.[InvDate], om.[GrandTotal], om.[OdrStatus], om.[PaymentMethod],
            c.[First_name], c.[last_name], c.[Company_Name], c.[Street_Address_1], c.[Street_Address_2], c.[City], c.[State], c.[Zip_Code], c.[Phone_Number], c.[EMail],
            oi.[Seqno], oi.[ItemNum], oi.[Quantity], oi.[Cost], oi.[Price], oi.[Tax], oi.[Non_Inventory_Name], oi.[KitID], oi.[Serial_Number],
            oi.[Note1], oi.[Note2], oi.[Note3], oi.[Note4], oi.[LineDisc], oi.[UpdateDate], oi.[RegPrice], oi.[PickQty], oi.[OverrideQty]
        FROM [SparkDB].[dbo].[OrderMaster] om
        LEFT JOIN [SparkDB].[dbo].[Customer] c ON om.[CustNum] = c.[CustNum]
        LEFT JOIN [SparkDB].[dbo].[OrderItems] oi ON om.[InvNum] = oi.[InvNum]
        WHERE (@OdrStatus IS NULL OR om.[OdrStatus] = @OdrStatus)
        ORDER BY om.[InvDate] DESC
        OFFSET @Offset ROWS FETCH NEXT @PerPage ROWS ONLY;
    ";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand command = new SqlCommand(invoiceQuery, conn))
                    {
                        // Add pagination parameters
                        int offset = (filter.Page - 1) * filter.PerPage;
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PerPage", filter.PerPage);

                        // If OdrStatus is provided, add it as a parameter; otherwise, don't add it
                        if (filter.OdrStatus.HasValue)
                        {
                            command.Parameters.AddWithValue("@OdrStatus", filter.OdrStatus.Value.ToString());
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@OdrStatus", DBNull.Value);
                        }

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            // Dictionary to keep track of invoices and their items
                            var invoiceDict = new Dictionary<string, Invoice>();

                            while (await reader.ReadAsync())
                            {
                                string invNum = reader["InvNum"].ToString();
                                if (!invoiceDict.ContainsKey(invNum))
                                {
                                    var invoice = new Invoice
                                    {
                                        InvNum = invNum,
                                        ShopID = reader["ShopID"].ToString(),
                                        CustNum = reader["CustNum"].ToString(),
                                        InvDate = reader["InvDate"].ToString(),
                                        GrandTotal = reader["GrandTotal"].ToString(),
                                        OdrStatus = reader["OdrStatus"].ToString(),
                                        PaymentMethod = reader["PaymentMethod"].ToString(),
                                        Customer = new CustomerDetail
                                        {
                                            FirstName = reader["First_name"].ToString(),
                                            LastName = reader["last_name"].ToString(),
                                            CompanyName = reader["Company_Name"].ToString(),
                                            StreetAddress1 = reader["Street_Address_1"].ToString(),
                                            StreetAddress2 = reader["Street_Address_2"].ToString(),
                                            City = reader["City"].ToString(),
                                            State = reader["State"].ToString(),
                                            ZipCode = reader["Zip_Code"].ToString(),
                                            PhoneNumber = reader["Phone_Number"].ToString(),
                                            Email = reader["EMail"].ToString()
                                        },
                                        Items = new List<InvoiceItem>()
                                    };

                                    invoiceDict[invNum] = invoice;
                                }

                                if (!reader.IsDBNull(reader.GetOrdinal("ItemNum")))
                                {
                                    var item = new InvoiceItem
                                    {
                                        Seqno = reader["Seqno"].ToString(),
                                        ItemNum = reader["ItemNum"].ToString(),
                                        Quantity = reader["Quantity"].ToString(),
                                        Cost = reader["Cost"].ToString(),
                                        Price = reader["Price"].ToString(),
                                        Tax = reader["Tax"].ToString(),
                                        NonInventoryName = reader["Non_Inventory_Name"].ToString(),
                                        KitID = reader["KitID"].ToString(),
                                        SerialNumber = reader["Serial_Number"].ToString(),
                                        Note1 = reader["Note1"].ToString(),
                                        Note2 = reader["Note2"].ToString(),
                                        Note3 = reader["Note3"].ToString(),
                                        Note4 = reader["Note4"].ToString(),
                                        LineDisc = reader["LineDisc"].ToString(),
                                        UpdateDate = reader["UpdateDate"].ToString(),
                                        RegPrice = reader["RegPrice"].ToString(),
                                        PickQty = reader["PickQty"].ToString(),
                                        OverrideQty = reader["OverrideQty"].ToString()
                                    };

                                    invoiceDict[invNum].Items.Add(item);
                                }
                            }

                            invoices = invoiceDict.Values.ToList();
                        }
                    }
                }

                return Ok(new { success = true, invoices });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /////////////////////////////////////////////////

        [HttpPost]
        [Route("ordersummary")]
        public async Task<IActionResult> GetOrderSummary([FromBody] OrderSummaryRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Optional filters
            string salesmanFilter = !string.IsNullOrEmpty(request.SalesmanName) ? "AND S.[Name] = @SalesmanName" : "";
            string areaNameFilter = !string.IsNullOrEmpty(request.AreaName) ? "AND A.[Name] = @AreaName" : "";

            // Last 7 months query
            string last7MonthsQuery = $@"
    WITH Months AS (
        SELECT FORMAT(DATEADD(MONTH, -number, GETDATE()), 'yyyy-MM') AS Month
        FROM master..spt_values
        WHERE type = 'P' AND number < 7
    )
    SELECT M.Month, 
           ISNULL(COUNT(OM.InvNum), 0) AS TotalOrders
    FROM Months M
    LEFT JOIN [SparkDB].[dbo].[OrderMaster] OM ON FORMAT(OM.InvDate, 'yyyy-MM') = M.Month
    LEFT JOIN [SparkDB].[dbo].[Customer] C ON OM.CustNum = C.CustNum
    LEFT JOIN [SparkDB].[dbo].[SalesArea] A ON C.AreaID = A.ID
    LEFT JOIN [SparkDB].[dbo].[Salesmans] S ON OM.CashierID = S.ID
    WHERE 1=1 {salesmanFilter} {areaNameFilter}
    GROUP BY M.Month
    ORDER BY M.Month ASC;";

            // Last 7 weeks query
            string last7WeeksQuery = $@"
    WITH Weeks AS (
        SELECT DATEPART(WEEK, DATEADD(WEEK, -number, GETDATE())) AS WeekNumber,
               YEAR(DATEADD(WEEK, -number, GETDATE())) AS Year
        FROM master..spt_values
        WHERE type = 'P' AND number < 7
    )
    SELECT W.Year, W.WeekNumber, 
           ISNULL(COUNT(OM.InvNum), 0) AS TotalOrders
    FROM Weeks W
    LEFT JOIN [SparkDB].[dbo].[OrderMaster] OM ON DATEPART(WEEK, OM.InvDate) = W.WeekNumber
    AND YEAR(OM.InvDate) = W.Year
    LEFT JOIN [SparkDB].[dbo].[Customer] C ON OM.CustNum = C.CustNum
    LEFT JOIN [SparkDB].[dbo].[SalesArea] A ON C.AreaID = A.ID
    LEFT JOIN [SparkDB].[dbo].[Salesmans] S ON OM.CashierID = S.ID
    WHERE 1=1 {salesmanFilter} {areaNameFilter}
    GROUP BY W.Year, W.WeekNumber
    ORDER BY W.Year DESC, W.WeekNumber ASC;";

            // Last 7 days query
            string last7DaysQuery = $@"
    WITH Days AS (
        SELECT CAST(DATEADD(DAY, -number, GETDATE()) AS DATE) AS Day
        FROM master..spt_values
        WHERE type = 'P' AND number < 7
    )
    SELECT D.Day, 
           ISNULL(COUNT(OM.InvNum), 0) AS TotalOrders
    FROM Days D
    LEFT JOIN [SparkDB].[dbo].[OrderMaster] OM ON CAST(OM.InvDate AS DATE) = D.Day
    LEFT JOIN [SparkDB].[dbo].[Customer] C ON OM.CustNum = C.CustNum
    LEFT JOIN [SparkDB].[dbo].[SalesArea] A ON C.AreaID = A.ID
    LEFT JOIN [SparkDB].[dbo].[Salesmans] S ON OM.CashierID = S.ID
    WHERE 1=1 {salesmanFilter} {areaNameFilter}
    GROUP BY D.Day
    ORDER BY D.Day ASC;";

            // Query to get distinct SalesmanNames
            string salesmanQuery = $@"
    SELECT DISTINCT S.[Name]
    FROM [SparkDB].[dbo].[Salesmans] S;";

            // Query to get distinct SalesAreas
            string saleAreaQuery = $@"
    SELECT [ID], 
           [Name]
    FROM [SparkDB].[dbo].[SalesArea];";

            string topSalesmanQuery = $@"
SELECT TOP 1 S.[Name], COUNT(OM.InvNum) AS TotalOrders
FROM [SparkDB].[dbo].[OrderMaster] OM
LEFT JOIN [SparkDB].[dbo].[Salesmans] S ON OM.CashierID = S.ID
WHERE FORMAT(OM.InvDate, 'yyyy-MM') = FORMAT(GETDATE(), 'yyyy-MM')
GROUP BY S.[Name]
ORDER BY TotalOrders DESC;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Execute queries
                    var last7Months = await ExecuteOrderSummaryQuery(conn, last7MonthsQuery, request.SalesmanName, request.AreaName);
                    var last7Weeks = await ExecuteOrderSummaryQuery(conn, last7WeeksQuery, request.SalesmanName, request.AreaName);
                    var last7Days = await ExecuteOrderSummaryQuery(conn, last7DaysQuery, request.SalesmanName, request.AreaName);
                    var distinctSalesmen = await ExecuteOrderSummaryQuery(conn, salesmanQuery);
                    var saleArea = await ExecuteOrderSummaryQuery(conn, saleAreaQuery);
                    var topSalesman = await ExecuteOrderSummaryQuery(conn, topSalesmanQuery);

                    return Ok(new
                    {
                        success = true,
                        last7Months = last7Months,
                        last7Weeks = last7Weeks,
                        last7Days = last7Days,
                        distinctSalesmen = distinctSalesmen,
                        saleArea = saleArea,
                        topSalesman = topSalesman.FirstOrDefault()
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        // Helper function to execute queries
        private async Task<List<object>> ExecuteOrderSummaryQuery(SqlConnection conn, string query, string salesmanName = null, string areaName = null)
        {
            List<object> result = new List<object>();

            using (SqlCommand command = new SqlCommand(query, conn))
            {
                if (salesmanName != null)
                {
                    command.Parameters.AddWithValue("@SalesmanName", salesmanName);
                }

                if (areaName != null)
                {
                    command.Parameters.AddWithValue("@AreaName", areaName);
                }

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var record = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            record[reader.GetName(i)] = reader.GetValue(i);
                        }

                        result.Add(record);
                    }
                }
            }

            return result;
        }

        //////////////////////////////////////////////////////

        [HttpPost("daily-orders-and-invoices")]
        public async Task<IActionResult> GetDailyOrdersAndInvoices()
        {
            List<RecentOrder> dailyOrders = new List<RecentOrder>();
            List<RecentOrder> dailyInvoices = new List<RecentOrder>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string ordersQuery = @"
        SELECT 
            o.[CustNum],
            CONCAT(c.[First_name], ' ', c.[Last_name]) AS CustName,
            c.[City],
            c.[State],
            o.[InvDate] AS OrderDate,
            o.[GrandTotal],
            sa.[Name] AS SalesmanName  -- Include Salesman's Name
        FROM 
            [SparkDB].[dbo].[OrderMaster] o
        JOIN 
            [SparkDB].[dbo].[Customer] c ON o.[CustNum] = c.[CustNum]
        LEFT JOIN 
            [SparkDB].[dbo].[SalesArea] sa ON o.[CashierID] = sa.[ID]  -- Join with SalesArea
        WHERE 
            CAST(o.[InvDate] AS DATE) = CAST(GETDATE() AS DATE)
        ORDER BY 
            o.[InvDate] DESC;
    ";

            string invoicesQuery = @"
        SELECT 
            o.[InvNum],          -- Include InvNum in the invoice query
            o.[CustNum],
            CONCAT(c.[First_name], ' ', c.[Last_name]) AS CustName,
            c.[City],
            c.[State],
            o.[InvDate] AS InvDate,
            o.[GrandTotal],
            sa.[Name] AS SalesmanName  -- Include Salesman's Name
        FROM 
            [SparkDB].[dbo].[OrderMaster] o
        JOIN 
            [SparkDB].[dbo].[Customer] c ON o.[CustNum] = c.[CustNum]
        LEFT JOIN 
            [SparkDB].[dbo].[SalesArea] sa ON o.[CashierID] = sa.[ID]  -- Join with SalesArea
        WHERE 
            CAST(o.[InvDate] AS DATE) = CAST(GETDATE() AS DATE)
        ORDER BY 
            o.[InvDate] DESC;
    ";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Fetch daily orders
                    using (SqlCommand command = new SqlCommand(ordersQuery, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dailyOrders.Add(new RecentOrder
                                {
                                    CustNum = reader["CustNum"].ToString(),
                                    CustName = reader["CustName"].ToString(),
                                    City = reader["City"].ToString(),
                                    State = reader["State"].ToString(),
                                    OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                                    GrandTotal = reader.GetDecimal(reader.GetOrdinal("GrandTotal")),
                                    SalesmanName = reader["SalesmanName"].ToString()
                                });
                            }
                        }
                    }

                    // Fetch daily invoices
                    using (SqlCommand command = new SqlCommand(invoicesQuery, conn))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dailyInvoices.Add(new RecentOrder
                                {
                                    InvNum = reader["InvNum"].ToString(),
                                    CustNum = reader["CustNum"].ToString(),
                                    CustName = reader["CustName"].ToString(),
                                    InvDate = reader.GetDateTime(reader.GetOrdinal("InvDate")),
                                    GrandTotal = reader.GetDecimal(reader.GetOrdinal("GrandTotal")),
                                    SalesmanName = reader["SalesmanName"].ToString()
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    orders = dailyOrders,
                    invoices = dailyInvoices
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }
    }
}